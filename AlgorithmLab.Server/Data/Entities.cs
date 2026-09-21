using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlgorithmLab.Server.Data;

[Table("experiments")]
public class ExperimentEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string AlgorithmId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AlgorithmName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ComplexityType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int NMin { get; set; }
    public int NMax { get; set; }
    public int Step { get; set; }
    public int RunsPerN { get; set; }

    public double? CFactor { get; set; }
    public double? MSE { get; set; }
    public double? RMSE { get; set; }
    public double? RSquared { get; set; }
    public double TotalDurationMs { get; set; }

    [MaxLength(64)]
    public string? ConfigHash { get; set; }

    [MaxLength(50)]
    public string StorageSource { get; set; } = "PostgreSQL 18";

    public List<ExperimentPointEntity> Points { get; set; } = new();
}

[Table("experiment_points")]
public class ExperimentPointEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExperimentId { get; set; }
    public ExperimentEntity? Experiment { get; set; }

    public int N { get; set; }
    public int M { get; set; }
    public double AvgMs { get; set; }
    public double MedianMs { get; set; }
    public double? TheoMs { get; set; }
    public long StepCount { get; set; }
    public bool IsOutlier { get; set; }

    public List<PointRunEntity> Runs { get; set; } = new();
}

[Table("point_runs")]
public class PointRunEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PointId { get; set; }
    public ExperimentPointEntity? Point { get; set; }

    public int RunIndex { get; set; }
    public double ElapsedMs { get; set; }
    public bool IsOutlier { get; set; }
}

[Table("benchmark_cache")]
public class BenchmarkCacheEntity
{
    [MaxLength(100)]
    public string AlgorithmId { get; set; } = string.Empty;

    public int N { get; set; }
    public int M { get; set; }

    [MaxLength(64)]
    public string ConfigHash { get; set; } = string.Empty;

    public double AvgMs { get; set; }
    public double MedianMs { get; set; }
    public long StepCount { get; set; }
    public string? RunsJson { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
