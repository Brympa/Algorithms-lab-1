using System.Net.Http.Json;
using System.Text.Json;
using AlgorithmLab.Core.Models;
using Microsoft.JSInterop;

namespace AlgorithmLab.Services;

public class HybridExperimentStorageService : IExperimentStorageService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly List<ExperimentRecord> _localExperiments = new();
    private readonly Dictionary<string, List<BenchmarkPoint>> _localCache = new();
    private bool _isInitialized = false;

    // Debounce фоновой записи localStorage (WASM single-thread — не пишем синхронно в hot path)
    private CancellationTokenSource? _persistCts;
    private bool _persistQueued;

    // Бюджет кэша: ограничиваем рост, чтобы JSON не упирался в quota ~5MB
    private const int MaxCacheKeys = 40;
    private const int MaxPointsPerKey = 400;
    private const int MaxHistoryCount = 30;
    private const int PersistDebounceMs = 400;

    public DatabaseStatus Status { get; private set; } = DatabaseStatus.Checking;
    public string StatusMessage { get; private set; } = "Проверка подключения к PostgreSQL 18...";
    public string ApiUrl { get; set; } = "http://localhost:5000/api";

    public event Action? OnStatusChanged;

    public HybridExperimentStorageService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            var histJson = await _js.InvokeAsync<string?>("localStorage.getItem", "algolab_history");
            if (!string.IsNullOrEmpty(histJson))
            {
                var list = JsonSerializer.Deserialize<List<ExperimentRecord>>(histJson);
                if (list != null && list.Count > 0)
                {
                    _localExperiments.Clear();
                    _localExperiments.AddRange(list);
                }
            }

            var cacheJson = await _js.InvokeAsync<string?>("localStorage.getItem", "algolab_cache");
            if (!string.IsNullOrEmpty(cacheJson))
            {
                var cache = JsonSerializer.Deserialize<Dictionary<string, List<BenchmarkPoint>>>(cacheJson);
                if (cache != null)
                {
                    foreach (var kvp in cache)
                    {
                        _localCache[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Флаг только после успешного чтения — иначе при prerender-ошибке кэш навсегда пуст
            _isInitialized = true;
        }
        catch
        {
            // Prerender / localStorage недоступен — не помечаем initialized
        }
    }

    public async Task CheckConnectionAsync()
    {
        await InitializeAsync();

        Status = DatabaseStatus.Checking;
        StatusMessage = "Подключение к API PostgreSQL 18...";
        OnStatusChanged?.Invoke();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var resp = await _http.GetAsync($"{ApiUrl}/experiments/health", cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                Status = DatabaseStatus.ConnectedPostgreSql;
                StatusMessage = "PostgreSQL 18: Онлайн (API подключен)";
            }
            else
            {
                Status = DatabaseStatus.LocalCacheActive;
                StatusMessage = "Автономный режим: Локальный кэш (API недоступен)";
            }
        }
        catch
        {
            Status = DatabaseStatus.LocalCacheActive;
            StatusMessage = "Автономный режим: Локальный кэш (PostgreSQL офлайн)";
        }

        OnStatusChanged?.Invoke();
    }

    public async Task<List<BenchmarkPoint>?> GetCachedPointsAsync(string algorithmId, string configHash)
    {
        await InitializeAsync();
        string cacheKey = $"{algorithmId}:{configHash}";

        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                var url = $"{ApiUrl}/experiments/cache?algorithmId={Uri.EscapeDataString(algorithmId)}&configHash={Uri.EscapeDataString(configHash)}";
                var cached = await _http.GetFromJsonAsync<List<BenchmarkPoint>>(url);
                if (cached != null && cached.Count > 0)
                {
                    return cached;
                }
            }
            catch
            {
                // При ошибке сетевого вызова читаем из локального кэша
            }
        }

        if (_localCache.TryGetValue(cacheKey, out var localPoints) && localPoints.Count > 0)
        {
            return localPoints;
        }

        return null;
    }

    public async Task<BenchmarkPoint?> GetCachedPointAsync(string algorithmId, int n, string configHash)
    {
        await InitializeAsync();
        string cacheKey = $"{algorithmId}:{configHash}";

        if (_localCache.TryGetValue(cacheKey, out var localPoints) && localPoints != null)
        {
            var found = localPoints.FirstOrDefault(p => p.N == n);
            if (found != null) return found;
        }

        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                var points = await GetCachedPointsAsync(algorithmId, configHash);
                if (points != null)
                {
                    if (!_localCache.TryGetValue(cacheKey, out var list))
                    {
                        list = new List<BenchmarkPoint>();
                        _localCache[cacheKey] = list;
                    }
                    foreach (var p in points)
                    {
                        list.RemoveAll(x => x.N == p.N && x.M == p.M);
                        list.Add(p);
                    }

                    var found = points.FirstOrDefault(p => p.N == n);
                    if (found != null) return found;
                }
            }
            catch { }
        }

        return null;
    }

    public async Task<Guid> SaveExperimentAsync(ExperimentRecord record)
    {
        await InitializeAsync();

        if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
        record.StorageSource = Status == DatabaseStatus.ConnectedPostgreSql ? "PostgreSQL 18" : "Локальный кэш";

        _localExperiments.RemoveAll(x => x.Id == record.Id);
        _localExperiments.Insert(0, record);

        string cacheKey = $"{record.AlgorithmId}:{record.ConfigHash}";
        if (!_localCache.TryGetValue(cacheKey, out var existingPoints))
        {
            existingPoints = new List<BenchmarkPoint>();
            _localCache[cacheKey] = existingPoints;
        }

        foreach (var pt in record.Points)
        {
            existingPoints.RemoveAll(p => p.N == pt.N && p.M == pt.M);
            existingPoints.Add(pt);
        }

        // Бюджет кэша/истории + debounced запись в localStorage (не блокирует hot path)
        TrimCaches();
        SchedulePersist();

        // Если онлайн — сохраняем в PostgreSQL (таймаут 3 с); вызывается из фона очереди
        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                using var saveCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var resp = await _http.PostAsJsonAsync($"{ApiUrl}/experiments", record, saveCts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    var id = await resp.Content.ReadFromJsonAsync<Guid>(cancellationToken: saveCts.Token);
                    return id != Guid.Empty ? id : record.Id;
                }
            }
            catch
            {
                // Таймаут или сбой соединения с сервером — результат уже сохранен локально
            }
        }

        return record.Id;
    }

    private void TrimCaches()
    {
        if (_localExperiments.Count > MaxHistoryCount)
        {
            _localExperiments.RemoveRange(MaxHistoryCount, _localExperiments.Count - MaxHistoryCount);
        }

        if (_localCache.Count > MaxCacheKeys)
        {
            var keys = _localCache.Keys.ToList();
            for (int i = MaxCacheKeys; i < keys.Count; i++)
            {
                _localCache.Remove(keys[i]);
            }
        }

        foreach (var key in _localCache.Keys.ToList())
        {
            var list = _localCache[key];
            if (list.Count <= MaxPointsPerKey) continue;

            var ordered = list.OrderBy(p => p.N).ToList();
            var stride = (double)ordered.Count / MaxPointsPerKey;
            var sampled = new List<BenchmarkPoint>(MaxPointsPerKey);
            for (int i = 0; i < MaxPointsPerKey; i++)
            {
                sampled.Add(ordered[(int)(i * stride)]);
            }
            if (sampled[^1].N != ordered[^1].N)
            {
                sampled[^1] = ordered[^1];
            }
            _localCache[key] = sampled;
        }
    }

    private void SchedulePersist()
    {
        _persistCts?.Cancel();
        _persistCts?.Dispose();
        _persistCts = new CancellationTokenSource();
        var token = _persistCts.Token;
        _ = PersistDebouncedAsync(token);
    }

    private async Task PersistDebouncedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(PersistDebounceMs, token);
            if (token.IsCancellationRequested) return;
            await PersistLocalAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[HybridStorage] Persist failed: {ex.Message}");
        }
    }

    private async Task PersistLocalAsync()
    {
        if (_persistQueued) return;
        _persistQueued = true;
        try
        {
            var compactHistory = _localExperiments.Take(MaxHistoryCount).Select(exp => new ExperimentRecord
            {
                Id = exp.Id,
                AlgorithmId = exp.AlgorithmId,
                AlgorithmName = exp.AlgorithmName,
                Category = exp.Category,
                Complexity = exp.Complexity,
                ComplexityDisplay = exp.ComplexityDisplay,
                CreatedAt = exp.CreatedAt,
                NMin = exp.NMin,
                NMax = exp.NMax,
                Step = exp.Step,
                RunsPerN = exp.RunsPerN,
                CFactor = exp.CFactor,
                MSE = exp.MSE,
                RMSE = exp.RMSE,
                RSquared = exp.RSquared,
                CV = exp.CV,
                TotalDurationMs = exp.TotalDurationMs,
                ConfigHash = exp.ConfigHash,
                StorageSource = exp.StorageSource,
                Points = exp.Points.Select(p => new BenchmarkPoint
                {
                    N = p.N,
                    M = p.M,
                    AvgMs = p.AvgMs,
                    MedianMs = p.MedianMs,
                    TheoMs = p.TheoMs,
                    StepCount = p.StepCount,
                    IsOutlier = p.IsOutlier
                }).ToList()
            }).ToList();

            var compactCache = new Dictionary<string, List<BenchmarkPoint>>();
            foreach (var kvp in _localCache)
            {
                compactCache[kvp.Key] = kvp.Value.Select(p => new BenchmarkPoint
                {
                    N = p.N,
                    M = p.M,
                    AvgMs = p.AvgMs,
                    MedianMs = p.MedianMs,
                    TheoMs = p.TheoMs,
                    StepCount = p.StepCount,
                    IsOutlier = p.IsOutlier
                }).ToList();
            }

            await _js.InvokeVoidAsync("localStorage.setItem", "algolab_history", JsonSerializer.Serialize(compactHistory));
            await _js.InvokeVoidAsync("localStorage.setItem", "algolab_cache", JsonSerializer.Serialize(compactCache));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HybridStorage] localStorage write: {ex.Message}");
        }
        finally
        {
            _persistQueued = false;
        }
    }

    public async Task<List<ExperimentRecord>> GetHistoryAsync()
    {
        await InitializeAsync();

        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                var list = await _http.GetFromJsonAsync<List<ExperimentRecord>>($"{ApiUrl}/experiments");
                if (list != null && list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        if (string.IsNullOrEmpty(item.StorageSource))
                        {
                            item.StorageSource = "PostgreSQL 18";
                        }
                    }
                    return list;
                }
            }
            catch
            {
                // Fallback to local
            }
        }

        foreach (var item in _localExperiments)
        {
            if (string.IsNullOrEmpty(item.StorageSource))
            {
                item.StorageSource = "Локальный кэш";
            }
        }

        return _localExperiments.ToList();
    }

    public async Task<ExperimentRecord?> GetExperimentByIdAsync(Guid id)
    {
        await InitializeAsync();

        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                var record = await _http.GetFromJsonAsync<ExperimentRecord>($"{ApiUrl}/experiments/{id}");
                if (record != null) return record;
            }
            catch
            {
                // Fallback to local
            }
        }

        return _localExperiments.FirstOrDefault(x => x.Id == id);
    }

    public async Task DeleteExperimentAsync(Guid id)
    {
        await InitializeAsync();
        _localExperiments.RemoveAll(x => x.Id == id);
        SchedulePersist();

        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                await _http.DeleteAsync($"{ApiUrl}/experiments/{id}");
            }
            catch { }
        }
    }

    public async Task ClearHistoryAsync()
    {
        _localExperiments.Clear();
        _localCache.Clear();

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "algolab_history");
            await _js.InvokeVoidAsync("localStorage.removeItem", "algolab_cache");
        }
        catch { }

        await Task.CompletedTask;
    }

    public async Task<DbConnectionTestResult> TestDbConnectionAsync(DbConnectionConfig config)
    {
        return await PostDbResultAsync($"{ApiUrl}/database/test", config, onConnected: null);
    }

    public async Task<DbConnectionTestResult> ApplyDbConnectionAsync(DbConnectionConfig config)
    {
        return await PostDbResultAsync($"{ApiUrl}/database/apply", config, onConnected: result =>
        {
            Status = DatabaseStatus.ConnectedPostgreSql;
            StatusMessage = $"PostgreSQL 18: {config.Database} ({config.Host}:{config.Port})";
            OnStatusChanged?.Invoke();
        });
    }

    /// <summary>
    /// POST с чтением тела не-2xx (ProblemDetails / DbConnectionTestResult),
    /// чтобы UI показывал реальную причину, а не голый «Ошибка HTTP 400».
    /// </summary>
    private async Task<DbConnectionTestResult> PostDbResultAsync(
        string url,
        DbConnectionConfig config,
        Action<DbConnectionTestResult>? onConnected)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(url, config);
            var body = await resp.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                if (resp.IsSuccessStatusCode)
                {
                    return new DbConnectionTestResult { Success = false, Message = "Пустой ответ от сервера" };
                }
                return new DbConnectionTestResult
                {
                    Success = false,
                    Message = $"Ошибка HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}"
                };
            }

            // 1) Ожидаемый контракт: DbConnectionTestResult
            try
            {
                var result = JsonSerializer.Deserialize<DbConnectionTestResult>(
                    body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result != null && (!string.IsNullOrEmpty(result.Message) || result.Success))
                {
                    if (result.Success)
                    {
                        onConnected?.Invoke(result);
                    }
                    return result;
                }
            }
            catch { /* не DTO — пробуем ProblemDetails */ }

            // 2) ProblemDetails / ValidationProblemDetails
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("title", out var titleEl) ||
                    doc.RootElement.TryGetProperty("detail", out titleEl) ||
                    doc.RootElement.TryGetProperty("message", out titleEl))
                {
                    return new DbConnectionTestResult
                    {
                        Success = false,
                        Message = $"Ошибка HTTP {(int)resp.StatusCode}: {titleEl.GetString()}"
                    };
                }
                if (doc.RootElement.TryGetProperty("errors", out _))
                {
                    return new DbConnectionTestResult
                    {
                        Success = false,
                        Message = $"Ошибка HTTP {(int)resp.StatusCode}: {body}"
                    };
                }
            }
            catch { /* не JSON */ }

            // 3) Прочий текст
            var snippet = body.Length > 300 ? body[..300] + "..." : body;
            return new DbConnectionTestResult
            {
                Success = false,
                Message = resp.IsSuccessStatusCode
                    ? snippet
                    : $"Ошибка HTTP {(int)resp.StatusCode}: {snippet}"
            };
        }
        catch (Exception ex)
        {
            return new DbConnectionTestResult { Success = false, Message = $"Сервер API недоступен: {ex.Message}" };
        }
    }
}
