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
        _isInitialized = true;

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
        }
        catch
        {
            // Prerender / localStorage недоступен
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

        // Если подключен к PostgreSQL 18 через API
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

        // Локальный кэш
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

        // 1. Быстрая проверка в локальной памяти/кэше
        if (_localCache.TryGetValue(cacheKey, out var localPoints) && localPoints != null)
        {
            var found = localPoints.FirstOrDefault(p => p.N == n);
            if (found != null) return found;
        }

        // 2. Если онлайн в PostgreSQL, проверяем там
        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                var points = await GetCachedPointsAsync(algorithmId, configHash);
                if (points != null)
                {
                    // Сохраняем в локальную память для последующих обращений
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

        // 1. Всегда сохраняем в локальное хранилище для мгновенного доступа
        _localExperiments.RemoveAll(x => x.Id == record.Id);
        _localExperiments.Insert(0, record);

        // Аккумулируем (сливаем) точки в кэше, чтобы не терять ранее вычисленные N
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

        // Персистентность в localStorage
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "algolab_history", JsonSerializer.Serialize(_localExperiments.Take(50)));
            await _js.InvokeVoidAsync("localStorage.setItem", "algolab_cache", JsonSerializer.Serialize(_localCache));
        }
        catch { }

        // 2. Если онлайн - сохраняем в PostgreSQL 18
        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{ApiUrl}/experiments", record);
                if (resp.IsSuccessStatusCode)
                {
                    var id = await resp.Content.ReadFromJsonAsync<Guid>();
                    return id != Guid.Empty ? id : record.Id;
                }
            }
            catch
            {
                // Ошибка отправки на сервер, сохранено локально
            }
        }

        return record.Id;
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

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "algolab_history", JsonSerializer.Serialize(_localExperiments.Take(50)));
        }
        catch { }

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
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ApiUrl}/database/test", config);
            if (resp.IsSuccessStatusCode)
            {
                return await resp.Content.ReadFromJsonAsync<DbConnectionTestResult>() 
                    ?? new DbConnectionTestResult { Success = false, Message = "Пустой ответ от сервера" };
            }
            return new DbConnectionTestResult { Success = false, Message = $"Ошибка HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}" };
        }
        catch (Exception ex)
        {
            return new DbConnectionTestResult { Success = false, Message = $"Сервер API недоступен: {ex.Message}" };
        }
    }

    public async Task<DbConnectionTestResult> ApplyDbConnectionAsync(DbConnectionConfig config)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ApiUrl}/database/apply", config);
            if (resp.IsSuccessStatusCode)
            {
                var result = await resp.Content.ReadFromJsonAsync<DbConnectionTestResult>();
                if (result?.Success == true)
                {
                    Status = DatabaseStatus.ConnectedPostgreSql;
                    StatusMessage = $"PostgreSQL 18: {config.Database} ({config.Host}:{config.Port})";
                    OnStatusChanged?.Invoke();
                }
                return result ?? new DbConnectionTestResult { Success = false, Message = "Пустой ответ" };
            }
            return new DbConnectionTestResult { Success = false, Message = $"Ошибка HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}" };
        }
        catch (Exception ex)
        {
            return new DbConnectionTestResult { Success = false, Message = $"Сервер API недоступен: {ex.Message}" };
        }
    }
}
