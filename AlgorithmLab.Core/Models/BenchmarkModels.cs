namespace AlgorithmLab.Core.Models;

public class PointRun
{
    public int RunIndex { get; set; }
    public double ElapsedMs { get; set; }
    public bool IsOutlier { get; set; }
}

public class BenchmarkPoint
{
    public int N { get; set; }
    public int M { get; set; }
    public double AvgMs { get; set; }
    public double MedianMs { get; set; }
    public double TheoMs { get; set; }
    public long StepCount { get; set; }
    public bool IsOutlier { get; set; }
    public List<PointRun> Runs { get; set; } = new();
}

public class ApproximationResult
{
    public double C { get; set; }
    public double MSE { get; set; }
    public double RMSE { get; set; }
    public double RSquared { get; set; }
    public double? CV { get; set; }
    public string FormulaDisplay { get; set; } = string.Empty;
}

public class ExperimentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AlgorithmId { get; set; } = string.Empty;
    public string AlgorithmName { get; set; } = string.Empty;
    public AlgorithmCategory Category { get; set; }
    public ComplexityType Complexity { get; set; }
    public string ComplexityDisplay { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int NMin { get; set; }
    public int NMax { get; set; }
    public int Step { get; set; }
    public int RunsPerN { get; set; }
    public double? CFactor { get; set; }
    public double? MSE { get; set; }
    public double? RMSE { get; set; }
    public double? RSquared { get; set; }
    public double? CV { get; set; }
    public double TotalDurationMs { get; set; }
    public string ConfigHash { get; set; } = string.Empty;
    public string StorageSource { get; set; } = "Локальный кэш";
    public List<BenchmarkPoint> Points { get; set; } = new();
}

public class StepExecutionResult
{
    public double Value { get; set; }
    public long StepCount { get; set; }
}

public class DbConnectionConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "algorithmlab";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "postgres";
    public string SslMode { get; set; } = "Prefer"; // Disable, Prefer, Require

    public string BuildConnectionString()
    {
        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SSL Mode={SslMode};Timeout=4;Command Timeout=5;";
    }
}

public class DbConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ServerVersion { get; set; }
    public double LatencyMs { get; set; }
}

