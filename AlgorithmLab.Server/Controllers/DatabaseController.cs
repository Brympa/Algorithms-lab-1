using System.Diagnostics;
using AlgorithmLab.Core.Models;
using AlgorithmLab.Server.Data;
using AlgorithmLab.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AlgorithmLab.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly DatabaseConnectionManager _connectionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseController> _logger;

    public DatabaseController(
        DatabaseConnectionManager connectionManager,
        IServiceProvider serviceProvider,
        ILogger<DatabaseController> logger)
    {
        _connectionManager = connectionManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var cfg = _connectionManager.ActiveConfig;
        bool canConnect = false;
        string version = string.Empty;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            canConnect = await db.Database.CanConnectAsync();
            if (canConnect)
            {
                await using var conn = new NpgsqlConnection(_connectionManager.ConnectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT version();", conn);
                var res = await cmd.ExecuteScalarAsync();
                version = res?.ToString() ?? "PostgreSQL";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("DB status check failed: {Message}", ex.Message);
        }

        return Ok(new
        {
            connected = canConnect,
            host = cfg.Host,
            port = cfg.Port,
            database = cfg.Database,
            username = cfg.Username,
            sslMode = cfg.SslMode,
            version = version
        });
    }

    [HttpPost("test")]
    public async Task<ActionResult<DbConnectionTestResult>> TestConnection([FromBody] DbConnectionConfig config)
    {
        if (config == null)
        {
            // Единый контракт: 200 + Success=false (как и при ошибке подключения)
            return Ok(new DbConnectionTestResult { Success = false, Message = "Конфигурация не передана" });
        }

        var result = await TestConnectionCoreAsync(config);
        if (result.Success)
        {
            return Ok(result);
        }

        _logger.LogWarning("Connection test failed for {Host}:{Port}/{Database}: {Message}",
            config.Host, config.Port, config.Database, result.Message);
        return Ok(result);
    }

    [HttpPost("apply")]
    public async Task<ActionResult<DbConnectionTestResult>> ApplyConnection([FromBody] DbConnectionConfig config)
    {
        if (config == null)
        {
            return Ok(new DbConnectionTestResult { Success = false, Message = "Конфигурация не передана" });
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // 1. Сначала тестируем (тот же code-path, что и ручной Test)
            var testResult = await TestConnectionCoreAsync(config);
            if (!testResult.Success)
            {
                _logger.LogWarning("Apply rejected — test failed for {Host}:{Port}/{Database}: {Message}",
                    config.Host, config.Port, config.Database, testResult.Message);
                return Ok(testResult);
            }

            // 2. Применяем
            _connectionManager.SetConnection(config);

            // 3. Создаём/дополняем схему (идемпотентно)
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await EnsureSchemaAsync(db);

            sw.Stop();
            return Ok(new DbConnectionTestResult
            {
                Success = true,
                Message = $"База данных {config.Database} успешно подключена и схема проверена",
                ServerVersion = testResult.ServerVersion,
                LatencyMs = testResult.LatencyMs
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Apply connection schema init failed for {Host}:{Port}/{Database}",
                config.Host, config.Port, config.Database);
            // Единый контракт: 200 + Success=false с понятным Message
            return Ok(new DbConnectionTestResult
            {
                Success = false,
                Message = $"Ошибка инициализации схемы: {ex.Message}",
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            });
        }
    }

    private async Task<DbConnectionTestResult> TestConnectionCoreAsync(DbConnectionConfig config)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var connStr = config.BuildConnectionString();
            await using var conn = new NpgsqlConnection(connStr);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await conn.OpenAsync(cts.Token);

            await using var cmd = new NpgsqlCommand("SELECT version();", conn);
            var versionObj = await cmd.ExecuteScalarAsync(cts.Token);
            sw.Stop();

            string versionStr = versionObj?.ToString() ?? "PostgreSQL";
            if (versionStr.Length > 80) versionStr = versionStr[..80] + "...";

            return new DbConnectionTestResult
            {
                Success = true,
                Message = $"Успешное подключение к {config.Database} на {config.Host}:{config.Port}",
                ServerVersion = versionStr,
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DbConnectionTestResult
            {
                Success = false,
                Message = $"Ошибка подключения: {ex.Message}",
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            };
        }
    }

    /// <summary>
    /// Идемпотентная инициализация схемы: EnsureCreated + ALTER для колонок,
    /// которых нет в init.sql, но есть в EF-модели.
    /// </summary>
    private static async Task EnsureSchemaAsync(AppDbContext db)
    {
        // На пустой БД создаст полную схему; на существующей — no-op
        await db.Database.EnsureCreatedAsync();

        // Докручиваем колонки, появившиеся в EF позже init.sql
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE experiments ADD COLUMN IF NOT EXISTS storage_source VARCHAR(50);");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE experiments ADD COLUMN IF NOT EXISTS cv DOUBLE PRECISION;");
        // B3: колонки кэша для outlier/theo при hit
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE benchmark_cache ADD COLUMN IF NOT EXISTS is_outlier BOOLEAN DEFAULT FALSE;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE benchmark_cache ADD COLUMN IF NOT EXISTS theo_ms DOUBLE PRECISION;");
    }
}
