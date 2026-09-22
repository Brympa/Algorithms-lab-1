using System.Text.Json;
using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Benchmark;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Core.Models;
using AlgorithmLab.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlgorithmLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BenchmarkController : ControllerBase
{
    private readonly IAlgorithmRegistry _registry;
    private readonly IMasterDatasetProvider _datasetProvider;
    private readonly AppDbContext _db;
    private readonly ILogger<BenchmarkController> _logger;

    public BenchmarkController(
        IAlgorithmRegistry registry,
        IMasterDatasetProvider datasetProvider,
        AppDbContext db,
        ILogger<BenchmarkController> logger)
    {
        _registry = registry;
        _datasetProvider = datasetProvider;
        _db = db;
        _logger = logger;
    }

    public class RunRequest
    {
        public string AlgorithmId { get; set; } = string.Empty;
        public int NMin { get; set; } = 10;
        public int NMax { get; set; } = 1000;
        public int Step { get; set; } = 50;
        public int RunsPerN { get; set; } = 5;
        public bool ForceRecalculate { get; set; }
        public string? ConfigHash { get; set; }
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunRequest req, CancellationToken ct)
    {
        var algo = _registry.GetById(req.AlgorithmId);
        if (algo == null)
        {
            return NotFound(new { error = $"Алгоритм '{req.AlgorithmId}' не найден." });
        }

        var engine = new PrecisionBenchmarkEngine(_datasetProvider);
        var (getCached, cacheContext) = await CreateCacheLookupAsync(req.AlgorithmId, req.ConfigHash, req.ForceRecalculate, ct);

        try
        {
            var result = await Task.Run(() => engine.RunExperimentAsync(
                algo, req.NMin, req.NMax, req.Step, req.RunsPerN,
                getCachedPoint: getCached, cancellationToken: ct), ct);

            await UpsertCacheAsync(req.AlgorithmId, req.ConfigHash, result, ct);
            return Ok(result);
        }
        finally
        {
            cacheContext?.Dispose();
        }
    }

    [HttpGet("stream")]
    public async Task StreamBenchmark(
        [FromQuery] string algorithmId,
        [FromQuery] int nMin = 10,
        [FromQuery] int nMax = 1000,
        [FromQuery] int step = 50,
        [FromQuery] int runsPerN = 5,
        [FromQuery] bool forceRecalculate = false,
        [FromQuery] string? configHash = null,
        CancellationToken ct = default)
    {
        var algo = _registry.GetById(algorithmId);
        if (algo == null)
        {
            Response.StatusCode = 404;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        // configHash: клиентский DatasetConfig hash; иначе — серверный провайдер
        var effectiveHash = string.IsNullOrWhiteSpace(configHash)
            ? _datasetProvider.Config.ComputeHash()
            : configHash;

        var engine = new PrecisionBenchmarkEngine(_datasetProvider);
        var (getCached, cacheContext) = await CreateCacheLookupAsync(algorithmId, effectiveHash, forceRecalculate, ct);

        ExperimentRecord result;
        try
        {
            result = await engine.RunExperimentAsync(
                algo, nMin, nMax, step, runsPerN,
                onPointComputed: async pt =>
                {
                    var json = JsonSerializer.Serialize(pt);
                    await Response.WriteAsync($"data: {json}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                },
                getCachedPoint: getCached,
                cancellationToken: ct);
        }
        finally
        {
            cacheContext?.Dispose();
        }

        // Write-through: сохраняем промахи/итог в benchmark_cache сразу, не дожидаясь POST клиента
        await UpsertCacheAsync(algorithmId, effectiveHash, result, ct);

        var finalJson = JsonSerializer.Serialize(result);
        await Response.WriteAsync($"event: complete\ndata: {finalJson}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    [HttpGet("info")]
    public IActionResult GetHostInfo()
    {
        string cpuName = DetectCpuModel();
        string shortCpu = FormatShortCpuName(cpuName);
        return Ok(new
        {
            cpuModel = cpuName,
            shortCpuName = shortCpu,
            processorCount = Environment.ProcessorCount,
            osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        });
    }

    /// <summary>
    /// Bulk-load кэша один раз. При недоступной БД — null lookup, stream не падает.
    /// </summary>
    private async Task<(Func<int, Task<BenchmarkPoint?>>? Lookup, IDisposable? Ctx)> CreateCacheLookupAsync(
        string algorithmId,
        string? configHash,
        bool forceRecalculate,
        CancellationToken ct)
    {
        if (forceRecalculate || string.IsNullOrWhiteSpace(configHash))
        {
            return (null, null);
        }

        try
        {
            var hash = configHash!;
            var entries = await _db.BenchmarkCache.AsNoTracking()
                .Where(c => c.AlgorithmId == algorithmId && c.ConfigHash == hash)
                .ToListAsync(ct);

            if (entries.Count == 0)
            {
                return (null, null);
            }

            var byN = entries
                .GroupBy(c => c.N)
                .ToDictionary(g => g.Key, g => g.First());

            Func<int, Task<BenchmarkPoint?>> lookup = n =>
            {
                if (!byN.TryGetValue(n, out var c))
                {
                    return Task.FromResult<BenchmarkPoint?>(null);
                }

                return Task.FromResult<BenchmarkPoint?>(new BenchmarkPoint
                {
                    N = c.N,
                    M = c.M,
                    AvgMs = c.AvgMs,
                    MedianMs = c.MedianMs,
                    TheoMs = c.TheoMs ?? 0,
                    StepCount = c.StepCount,
                    IsOutlier = c.IsOutlier,
                    Runs = !string.IsNullOrEmpty(c.RunsJson)
                        ? JsonSerializer.Deserialize<List<PointRun>>(c.RunsJson) ?? new()
                        : new()
                });
            };

            return (lookup, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cache load failed (continuing without cache): {Message}", ex.Message);
            return (null, null);
        }
    }

    private async Task UpsertCacheAsync(
        string algorithmId,
        string? configHash,
        ExperimentRecord result,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configHash) || result?.Points == null)
        {
            return;
        }

        var hash = configHash!;
        try
        {
            var nValues = result.Points.Select(p => p.N).ToList();
            var existing = await _db.BenchmarkCache
                .Where(c => c.AlgorithmId == algorithmId && c.ConfigHash == hash && nValues.Contains(c.N))
                .ToListAsync(ct);

            var byN = existing.ToDictionary(c => c.N);

            foreach (var p in result.Points)
            {
                if (!byN.TryGetValue(p.N, out var entity))
                {
                    entity = new BenchmarkCacheEntity
                    {
                        AlgorithmId = algorithmId,
                        N = p.N,
                        M = p.M,
                        ConfigHash = hash
                    };
                    _db.BenchmarkCache.Add(entity);
                }

                entity.AvgMs = p.AvgMs;
                entity.MedianMs = p.MedianMs;
                entity.StepCount = p.StepCount;
                entity.IsOutlier = p.IsOutlier;
                entity.TheoMs = p.TheoMs;
                entity.RunsJson = JsonSerializer.Serialize(p.Runs);
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cache write-through failed: {Message}", ex.Message);
        }
    }

    private static string DetectCpuModel()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                var name = key?.GetValue("ProcessorNameString") as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name.Trim();
                }
            }
            else if (OperatingSystem.IsLinux() && System.IO.File.Exists("/proc/cpuinfo"))
            {
                var lines = System.IO.File.ReadAllLines("/proc/cpuinfo");
                var line = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                if (line != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1) return parts[1].Trim();
                }
            }
        }
        catch
        {
        }

        return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
    }

    private static string FormatShortCpuName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "Host CPU";

        var match = System.Text.RegularExpressions.Regex.Match(fullName, @"(i[3579]-\w+|Ryzen\s+\d+\s+\w+|Apple\s+M\d+|Xeon\s+\w+|M\d+\s+(?:Pro|Max|Ultra)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Value;
        }

        var cleaned = fullName
            .Replace("(R)", "")
            .Replace("(TM)", "")
            .Replace("Processor", "")
            .Replace("CPU", "")
            .Trim();

        var atIndex = cleaned.IndexOf('@');
        if (atIndex > 0) cleaned = cleaned.Substring(0, atIndex).Trim();

        return cleaned.Length > 22 ? cleaned.Substring(0, 22).Trim() : cleaned;
    }
}
