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
            return BadRequest(new DbConnectionTestResult { Success = false, Message = "Конфигурация не передана" });
        }

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

            return Ok(new DbConnectionTestResult
            {
                Success = true,
                Message = $"Успешное подключение к {config.Database} на {config.Host}:{config.Port}",
                ServerVersion = versionStr,
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning("Connection test failed: {Message}", ex.Message);
            return Ok(new DbConnectionTestResult
            {
                Success = false,
                Message = $"Ошибка подключения: {ex.Message}",
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            });
        }
    }

    [HttpPost("apply")]
    public async Task<ActionResult<DbConnectionTestResult>> ApplyConnection([FromBody] DbConnectionConfig config)
    {
        if (config == null) return BadRequest("Конфигурация не передана");

        // 1. Сначала тестируем
        var testResult = (await TestConnection(config)).Value;
        if (testResult == null || !testResult.Success)
        {
            return BadRequest(testResult ?? new DbConnectionTestResult { Success = false, Message = "Тест подключения провален" });
        }

        try
        {
            // 2. Применяем
            _connectionManager.SetConnection(config);

            // 3. Создаем схему таблиц, если ее еще нет
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

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
            return StatusCode(500, new DbConnectionTestResult
            {
                Success = false,
                Message = $"Ошибка инициализации схемы: {ex.Message}"
            });
        }
    }
}
