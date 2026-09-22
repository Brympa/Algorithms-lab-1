using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Benchmark;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AlgorithmLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BenchmarkController : ControllerBase
{
    private readonly IAlgorithmRegistry _registry;
    private readonly IMasterDatasetProvider _datasetProvider;

    public BenchmarkController(
        IAlgorithmRegistry registry,
        IMasterDatasetProvider datasetProvider)
    {
        _registry = registry;
        _datasetProvider = datasetProvider;
    }

    public class RunRequest
    {
        public string AlgorithmId { get; set; } = string.Empty;
        public int NMin { get; set; } = 10;
        public int NMax { get; set; } = 1000;
        public int Step { get; set; } = 50;
        public int RunsPerN { get; set; } = 5;
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
        var result = await Task.Run(() => engine.RunExperimentAsync(
            algo, req.NMin, req.NMax, req.Step, req.RunsPerN, cancellationToken: ct), ct);

        return Ok(result);
    }

    [HttpGet("stream")]
    public async Task StreamBenchmark(
        [FromQuery] string algorithmId,
        [FromQuery] int nMin = 10,
        [FromQuery] int nMax = 1000,
        [FromQuery] int step = 50,
        [FromQuery] int runsPerN = 5,
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

        var engine = new PrecisionBenchmarkEngine(_datasetProvider);

        var result = await engine.RunExperimentAsync(
            algo, nMin, nMax, step, runsPerN,
            onPointComputed: async pt =>
            {
                var json = System.Text.Json.JsonSerializer.Serialize(pt);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            },
            cancellationToken: ct
        );

        var finalJson = System.Text.Json.JsonSerializer.Serialize(result);
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
