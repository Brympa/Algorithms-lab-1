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

    public DatabaseStatus Status { get; private set; } = DatabaseStatus.Checking;
    public string StatusMessage { get; private set; } = "Проверка подключения к PostgreSQL 18...";
    public string ApiUrl { get; set; } = "http://localhost:5000/api";

    public event Action? OnStatusChanged;

    public HybridExperimentStorageService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task CheckConnectionAsync()
    {
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

    public async Task<Guid> SaveExperimentAsync(ExperimentRecord record)
    {
        if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();

        // 1. Всегда сохраняем в локальное хранилище для мгновенного доступа
        _localExperiments.RemoveAll(x => x.Id == record.Id);
        _localExperiments.Insert(0, record);

        string cacheKey = $"{record.AlgorithmId}:{record.ConfigHash}";
        _localCache[cacheKey] = record.Points;

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
        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            try
            {
                var list = await _http.GetFromJsonAsync<List<ExperimentRecord>>($"{ApiUrl}/experiments");
                if (list != null && list.Count > 0)
                {
                    return list;
                }
            }
            catch
            {
                // Fallback to local
            }
        }

        return _localExperiments.ToList();
    }

    public async Task<ExperimentRecord?> GetExperimentByIdAsync(Guid id)
    {
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
        _localExperiments.RemoveAll(x => x.Id == id);

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

        if (Status == DatabaseStatus.ConnectedPostgreSql)
        {
            // Можно очистить при необходимости
        }
        await Task.CompletedTask;
    }
}
