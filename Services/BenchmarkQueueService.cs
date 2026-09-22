using System.Net.Http.Json;
using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Benchmark;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Services;

public enum ExecutionEngineMode
{
    ServerNative,
    BrowserWasm
}

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
    private readonly HttpClient _http;
    private readonly List<BenchmarkQueueItem> _queue = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _currentCts;
    private bool _isProcessing = false;

    public ExecutionEngineMode EngineMode { get; set; } = ExecutionEngineMode.ServerNative;

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

    public string DetectedCpuName { get; private set; } = "Host CPU";
    public string DetectedCpuFullName { get; private set; } = "";
    public int DetectedCores { get; private set; } = Environment.ProcessorCount;

    public class HostInfoResponse
    {
        public string? CpuModel { get; set; }
        public string? ShortCpuName { get; set; }
        public int ProcessorCount { get; set; }
        public string? OsDescription { get; set; }
        public string? FrameworkDescription { get; set; }
    }

    public async Task RefreshHostInfoAsync()
    {
        try
        {
            var info = await _http.GetFromJsonAsync<HostInfoResponse>("http://localhost:5000/api/benchmark/info");
            if (info != null && !string.IsNullOrWhiteSpace(info.ShortCpuName))
            {
                DetectedCpuName = info.ShortCpuName;
                DetectedCpuFullName = info.CpuModel ?? info.ShortCpuName;
                if (info.ProcessorCount > 0)
                {
                    DetectedCores = info.ProcessorCount;
                }
                OnQueueChanged?.Invoke();
            }
        }
        catch
        {
            // Fallback если сервер офлайн
        }
    }

    public BenchmarkQueueService(
        PrecisionBenchmarkEngine benchmarkEngine,
        IMasterDatasetProvider datasetProvider,
        IExperimentStorageService storage,
        HttpClient http)
    {
        _benchmarkEngine = benchmarkEngine;
        _datasetProvider = datasetProvider;
        _storage = storage;
        _http = http;

        _ = RefreshHostInfoAsync();
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
                    ExperimentRecord? result = null;
                    bool receivedAnyStreamPoint = false;

                    // 1. Попытка нативного потокового выполнения на сервере (ASP.NET Core / Multi-core CPU)
                    if (EngineMode == ExecutionEngineMode.ServerNative)
                    {
                        try
                        {
                            var query =
                                $"http://localhost:5000/api/benchmark/stream" +
                                $"?algorithmId={Uri.EscapeDataString(nextItem.Algorithm.Id)}" +
                                $"&nMin={nextItem.NMin}&nMax={nextItem.NMax}&step={nextItem.Step}&runsPerN={nextItem.RunsPerN}" +
                                $"&forceRecalculate={(nextItem.ForceRecalculate ? "true" : "false")}" +
                                $"&configHash={Uri.EscapeDataString(configHash)}";
                            using var req = new HttpRequestMessage(HttpMethod.Get, query);
                            Microsoft.AspNetCore.Components.WebAssembly.Http.WebAssemblyHttpRequestMessageExtensions.SetBrowserResponseStreamingEnabled(req, true);
                            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, _currentCts.Token);

                            if (resp.IsSuccessStatusCode)
                            {
                                using var stream = await resp.Content.ReadAsStreamAsync(_currentCts.Token);
                                using var reader = new StreamReader(stream);

                                var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                bool isCompleteEvent = false;
                                // Нормализуем CRLF: "event: complete\r" не должен ломать StartsWith
                                string? line;
                                while ((line = await reader.ReadLineAsync(_currentCts.Token)) != null)
                                {
                                    if (_currentCts.Token.IsCancellationRequested) break;
                                    line = line.TrimEnd('\r');
                                    if (string.IsNullOrWhiteSpace(line)) continue;

                                    if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isCompleteEvent = line.Contains("complete", StringComparison.OrdinalIgnoreCase);
                                    }
                                    else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var json = line.Substring(line.IndexOf(':') + 1).Trim();
                                        if (isCompleteEvent)
                                        {
                                            result = System.Text.Json.JsonSerializer.Deserialize<ExperimentRecord>(json, jsonOptions);
                                            break;
                                        }
                                        else
                                        {
                                            var pt = System.Text.Json.JsonSerializer.Deserialize<BenchmarkPoint>(json, jsonOptions);
                                            if (pt != null)
                                            {
                                                receivedAnyStreamPoint = true;
                                                nextItem.CurrentProgressN = pt.N;
                                                if (pt.IsOutlier) nextItem.OutliersCount++;
                                                OnPointComputed?.Invoke(nextItem, pt);
                                                await Task.Yield();
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[BenchmarkQueueService] Stream HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // Если сервер недоступен — бесшовно переключаемся на локальный движок WASM
                            Console.WriteLine($"[BenchmarkQueueService] Stream fallback: {ex.Message}");
                            result = null;
                        }
                    }

                    // 2. Локальное выполнение в WebAssembly, если сервер не использовался
                    if (result == null)
                    {
                        // Частичный stream без complete: не дозапускать WASM поверх уже нарисованных точек —
                        // иначе будут дубли. В этом случае считаем stream прерванным и поднимаем исключение.
                        if (receivedAnyStreamPoint)
                        {
                            throw new InvalidOperationException(
                                "Потоковый расчёт на сервере прерван до complete — результат неполный.");
                        }

                        result = await _benchmarkEngine.RunExperimentAsync(
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
                    }

                    nextItem.Result = result;
                    nextItem.TotalDurationMs = result.TotalDurationMs;
                    nextItem.Status = QueueItemStatus.Completed;
                    nextItem.CurrentProgressN = nextItem.NMax;

                    // UI сразу, без ожидания Save (Save уходит в debounce-фон storage)
                    CurrentRunningItem = null;
                    OnItemCompleted?.Invoke(nextItem);
                    OnQueueChanged?.Invoke();
                    await Task.Yield();

                    // Фоновое сохранение — НЕ блокирует следующий item очереди
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _storage.SaveExperimentAsync(result);
                        }
                        catch (Exception saveEx)
                        {
                            Console.WriteLine($"[BenchmarkQueueService] Warning: SaveExperimentAsync: {saveEx.Message}");
                        }
                    });
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
                    // OnItemCompleted уже вызван на success — не дублируем;
                    // на fail/cancel CurrentRunningItem ещё указывает на nextItem
                    if (CurrentRunningItem == nextItem)
                    {
                        CurrentRunningItem = null;
                        OnItemCompleted?.Invoke(nextItem);
                    }
                    // OnQueueChanged уже был на success — дублируем только если не было
                    if (nextItem.Status != QueueItemStatus.Completed)
                    {
                        OnQueueChanged?.Invoke();
                    }
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
