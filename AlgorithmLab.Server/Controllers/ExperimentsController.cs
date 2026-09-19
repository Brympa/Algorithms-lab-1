using System.Text.Json;
using AlgorithmLab.Core.Models;
using AlgorithmLab.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlgorithmLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperimentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExperimentsController> _logger;

    public ExperimentsController(AppDbContext db, ILogger<ExperimentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        try
        {
            bool canConnect = await _db.Database.CanConnectAsync();
            return Ok(new { status = "healthy", database = canConnect ? "connected" : "disconnected", version = "PostgreSQL 18" });
        }
        catch (Exception ex)
        {
            return Ok(new { status = "degraded", database = "error", error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ExperimentRecord>>> GetAll()
    {
        var entities = await _db.Experiments
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync();

        var records = entities.Select(e => new ExperimentRecord
        {
            Id = e.Id,
            AlgorithmId = e.AlgorithmId,
            AlgorithmName = e.AlgorithmName,
            Category = Enum.TryParse<AlgorithmCategory>(e.Category, out var cat) ? cat : AlgorithmCategory.Vectors,
            Complexity = Enum.TryParse<ComplexityType>(e.ComplexityType, out var comp) ? comp : ComplexityType.ON,
            CreatedAt = e.CreatedAt,
            NMin = e.NMin,
            NMax = e.NMax,
            Step = e.Step,
            RunsPerN = e.RunsPerN,
            CFactor = e.CFactor,
            MSE = e.MSE,
            RMSE = e.RMSE,
            RSquared = e.RSquared,
            ConfigHash = e.ConfigHash ?? string.Empty,
            StorageSource = string.IsNullOrEmpty(e.StorageSource) ? "PostgreSQL 18" : e.StorageSource
        }).ToList();

        return Ok(records);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExperimentRecord>> GetById(Guid id)
    {
        var e = await _db.Experiments
            .Include(x => x.Points)
            .ThenInclude(p => p.Runs)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (e == null) return NotFound();

        var record = new ExperimentRecord
        {
            Id = e.Id,
            AlgorithmId = e.AlgorithmId,
            AlgorithmName = e.AlgorithmName,
            Category = Enum.TryParse<AlgorithmCategory>(e.Category, out var cat) ? cat : AlgorithmCategory.Vectors,
            Complexity = Enum.TryParse<ComplexityType>(e.ComplexityType, out var comp) ? comp : ComplexityType.ON,
            CreatedAt = e.CreatedAt,
            NMin = e.NMin,
            NMax = e.NMax,
            Step = e.Step,
            RunsPerN = e.RunsPerN,
            CFactor = e.CFactor,
            MSE = e.MSE,
            RMSE = e.RMSE,
            RSquared = e.RSquared,
            ConfigHash = e.ConfigHash ?? string.Empty,
            StorageSource = string.IsNullOrEmpty(e.StorageSource) ? "PostgreSQL 18" : e.StorageSource,
            Points = e.Points.OrderBy(p => p.N).Select(p => new BenchmarkPoint
            {
                N = p.N,
                M = p.M,
                AvgMs = p.AvgMs,
                MedianMs = p.MedianMs,
                TheoMs = p.TheoMs ?? 0,
                StepCount = p.StepCount,
                IsOutlier = p.IsOutlier,
                Runs = p.Runs.OrderBy(r => r.RunIndex).Select(r => new PointRun
                {
                    RunIndex = r.RunIndex,
                    ElapsedMs = r.ElapsedMs,
                    IsOutlier = r.IsOutlier
                }).ToList()
            }).ToList()
        };

        return Ok(record);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Save([FromBody] ExperimentRecord record)
    {
        if (record == null) return BadRequest();

        var entity = new ExperimentEntity
        {
            Id = record.Id == Guid.Empty ? Guid.NewGuid() : record.Id,
            AlgorithmId = record.AlgorithmId,
            AlgorithmName = record.AlgorithmName,
            Category = record.Category.ToString(),
            ComplexityType = record.Complexity.ToString(),
            CreatedAt = record.CreatedAt == default ? DateTime.UtcNow : record.CreatedAt,
            NMin = record.NMin,
            NMax = record.NMax,
            Step = record.Step,
            RunsPerN = record.RunsPerN,
            CFactor = record.CFactor,
            MSE = record.MSE,
            RMSE = record.RMSE,
            RSquared = record.RSquared,
            ConfigHash = record.ConfigHash,
            StorageSource = string.IsNullOrEmpty(record.StorageSource) ? "PostgreSQL 18" : record.StorageSource
        };

        foreach (var p in record.Points)
        {
            var pointEntity = new ExperimentPointEntity
            {
                Id = Guid.NewGuid(),
                ExperimentId = entity.Id,
                N = p.N,
                M = p.M,
                AvgMs = p.AvgMs,
                MedianMs = p.MedianMs,
                TheoMs = p.TheoMs,
                StepCount = p.StepCount,
                IsOutlier = p.IsOutlier
            };

            foreach (var r in p.Runs)
            {
                pointEntity.Runs.Add(new PointRunEntity
                {
                    Id = Guid.NewGuid(),
                    PointId = pointEntity.Id,
                    RunIndex = r.RunIndex,
                    ElapsedMs = r.ElapsedMs,
                    IsOutlier = r.IsOutlier
                });
            }

            entity.Points.Add(pointEntity);

            // Кэширование
            var cache = await _db.BenchmarkCache.FindAsync(entity.AlgorithmId, p.N, p.M, entity.ConfigHash ?? "");
            if (cache == null)
            {
                _db.BenchmarkCache.Add(new BenchmarkCacheEntity
                {
                    AlgorithmId = entity.AlgorithmId,
                    N = p.N,
                    M = p.M,
                    ConfigHash = entity.ConfigHash ?? "",
                    AvgMs = p.AvgMs,
                    MedianMs = p.MedianMs,
                    StepCount = p.StepCount,
                    RunsJson = JsonSerializer.Serialize(p.Runs),
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        _db.Experiments.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(entity.Id);
    }

    [HttpGet("cache")]
    public async Task<ActionResult<List<BenchmarkPoint>>> GetCache(
        [FromQuery] string algorithmId,
        [FromQuery] string configHash)
    {
        var entries = await _db.BenchmarkCache
            .Where(c => c.AlgorithmId == algorithmId && c.ConfigHash == configHash)
            .OrderBy(c => c.N)
            .ToListAsync();

        var points = entries.Select(c => new BenchmarkPoint
        {
            N = c.N,
            M = c.M,
            AvgMs = c.AvgMs,
            MedianMs = c.MedianMs,
            StepCount = c.StepCount,
            Runs = !string.IsNullOrEmpty(c.RunsJson)
                ? JsonSerializer.Deserialize<List<PointRun>>(c.RunsJson) ?? new()
                : new()
        }).ToList();

        return Ok(points);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var e = await _db.Experiments.FindAsync(id);
        if (e == null) return NotFound();
        _db.Experiments.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
