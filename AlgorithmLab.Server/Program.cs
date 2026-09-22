using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Server.Data;
using AlgorithmLab.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<IAlgorithmRegistry, AlgorithmRegistry>();
builder.Services.AddSingleton<IMasterDatasetProvider, MasterDatasetProvider>();

var connMgr = new DatabaseConnectionManager();
builder.Services.AddSingleton(connMgr);

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var mgr = sp.GetRequiredService<DatabaseConnectionManager>();
    options.UseNpgsql(mgr.ConnectionString);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Автоматическое создание схемы при запуске, если БД доступна
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.CanConnect())
    {
        db.Database.EnsureCreated();
        Console.WriteLine("[AlgorithmLab.Server] PostgreSQL 18 подключен, схема инициализирована.");
    }
    else
    {
        Console.WriteLine("[AlgorithmLab.Server] Предупреждение: PostgreSQL 18 недоступен. Запустите docker-compose up -d.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[AlgorithmLab.Server] Ошибка при проверке БД: {ex.Message}");
}

app.Run();
