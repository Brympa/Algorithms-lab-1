using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Benchmark;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Services;

public enum QueueItemStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled
}

public class BenchmarkQueueItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public IAlgorithm Algorithm { get; init; } = null!;
    public int NMin { get; init; }
    public int NMax { get; init; }
    public int Step { get; init; }
    public int RunsPerN { get; init; }
    public bool ForceRecalculate { get; init; }
    public DateTime QueuedAt { get; init; } = DateTime.UtcNow;

    public QueueItemStatus Status { get; set; } = QueueItemStatus.Queued;
    public int CurrentProgressN { get; set; }
    public int OutliersCount { get; set; }
    public double TotalDurationMs { get; set; }
    public ExperimentRecord? Result { get; set; }
    public string? ErrorMessage { get; set; }

    public double CalculateProgressPercent()
    {
        if (NMax <= NMin) return 100.0;
        double progress = (double)(CurrentProgressN - NMin) / (NMax - NMin) * 100.0;
        return Math.Clamp(progress, 0.0, 100.0);
    }
}

public class BenchmarkQueueService
{
    private readonly PrecisionBenchmarkEngine _benchmarkEngine;
    private readonly IMasterDatasetProvider _datasetProvider;
    private readonly IExperimentStorageService _storage;
    private readonly List<BenchmarkQueueItem> _queue = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _currentCts;
    private bool _isProcessing = false;

    public IReadOnlyList<BenchmarkQueueItem> Items
    {
        get
        {
            lock (_lock)
            {
                return _queue.ToList();
            }
        }
    }

    public BenchmarkQueueItem? CurrentRunningItem { get; private set; }
    public bool IsRunning => CurrentRunningItem != null;

    public event Action? OnQueueChanged;
    public event Action<BenchmarkQueueItem>? OnItemStarted;
    public event Action<BenchmarkQueueItem, BenchmarkPoint>? OnPointComputed;
    public event Action<BenchmarkQueueItem>? OnItemCompleted;

    public BenchmarkQueueService(
        PrecisionBenchmarkEngine benchmarkEngine,
        IMasterDatasetProvider datasetProvider,
        IExperimentStorageService storage)
    {
        _benchmarkEngine = benchmarkEngine;
        _datasetProvider = datasetProvider;
        _storage = storage;
    }

    public Guid Enqueue(
        IAlgorithm algorithm,
        int nMin,
        int nMax,
        int step,
        int runsPerN,
        bool forceRecalculate)
    {
        var item = new BenchmarkQueueItem
        {
            Algorithm = algorithm,
            NMin = nMin,
            NMax = nMax,
            Step = step,
            RunsPerN = runsPerN,
            ForceRecalculate = forceRecalculate,
            CurrentProgressN = nMin
        };

        lock (_lock)
        {
            _queue.Add(item);
        }

        OnQueueChanged?.Invoke();
        _ = ProcessQueueAsync();

        return item.Id;
    }

    public void CancelCurrent()
    {
        _currentCts?.Cancel();
    }

    public void RemoveItem(Guid id)
    {
        lock (_lock)
        {
            var item = _queue.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                if (item.Status == QueueItemStatus.Running)
                {
                    CancelCurrent();
                }
                _queue.Remove(item);
            }
        }
        OnQueueChanged?.Invoke();
    }

    public void ClearQueue()
    {
        lock (_lock)
        {
            var running = _queue.FirstOrDefault(x => x.Status == QueueItemStatus.Running);
            if (running != null)
            {
                CancelCurrent();
            }
            _queue.RemoveAll(x => x.Status == QueueItemStatus.Queued);
        }
        OnQueueChanged?.Invoke();
    }

    private async Task ProcessQueueAsync()
    {
        lock (_lock)
        {
            if (_isProcessing) return;
            _isProcessing = true;
        }

        try
        {
            while (true)
            {
                BenchmarkQueueItem? nextItem = null;
                lock (_lock)
                {
                    nextItem = _queue.FirstOrDefault(x => x.Status == QueueItemStatus.Queued);
                    if (nextItem == null)
                    {
                        _isProcessing = false;
                        CurrentRunningItem = null;
                        OnQueueChanged?.Invoke();
                        break;
                    }
                    nextItem.Status = QueueItemStatus.Running;
                    CurrentRunningItem = nextItem;
                }

                _currentCts = new CancellationTokenSource();
                OnQueueChanged?.Invoke();
                OnItemStarted?.Invoke(nextItem);

                try
                {
                    string configHash = _datasetProvider.Config.ComputeHash();

                    var result = await _benchmarkEngine.RunExperimentAsync(
                        nextItem.Algorithm,
                        nextItem.NMin,
                        nextItem.NMax,
                        nextItem.Step,
                        nextItem.RunsPerN,
                        async pt =>
                        {
                            nextItem.CurrentProgressN = pt.N;
                            if (pt.IsOutlier) nextItem.OutliersCount++;
                            OnPointComputed?.Invoke(nextItem, pt);
                            await Task.Yield();
                        },
                        getCachedPoint: nextItem.ForceRecalculate ? null : async n =>
                        {
                            return await _storage.GetCachedPointAsync(nextItem.Algorithm.Id, n, configHash);
                        },
                        cancellationToken: _currentCts.Token
                    );

                    nextItem.Result = result;
                    nextItem.TotalDurationMs = result.TotalDurationMs;
                    nextItem.Status = QueueItemStatus.Completed;

                    await _storage.SaveExperimentAsync(result);
                }
                catch (OperationCanceledException)
                {
                    nextItem.Status = QueueItemStatus.Canceled;
                }
                catch (Exception ex)
                {
                    nextItem.Status = QueueItemStatus.Failed;
                    nextItem.ErrorMessage = ex.Message;
                }
                finally
                {
                    _currentCts?.Dispose();
                    _currentCts = null;
                    CurrentRunningItem = null;
                    OnItemCompleted?.Invoke(nextItem);
                    OnQueueChanged?.Invoke();
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _isProcessing = false;
                CurrentRunningItem = null;
            }
            OnQueueChanged?.Invoke();
        }
    }
}
