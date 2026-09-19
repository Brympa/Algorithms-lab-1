using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Services;

public enum DatabaseStatus
{
    Checking,
    ConnectedPostgreSql,
    LocalCacheActive,
    Error
}

public interface IExperimentStorageService
{
    DatabaseStatus Status { get; }
    string StatusMessage { get; }
    string ApiUrl { get; set; }
    event Action? OnStatusChanged;

    Task CheckConnectionAsync();
    Task<List<BenchmarkPoint>?> GetCachedPointsAsync(string algorithmId, string configHash);
    Task<Guid> SaveExperimentAsync(ExperimentRecord record);
    Task<List<ExperimentRecord>> GetHistoryAsync();
    Task<ExperimentRecord?> GetExperimentByIdAsync(Guid id);
    Task DeleteExperimentAsync(Guid id);
    Task ClearHistoryAsync();
}
