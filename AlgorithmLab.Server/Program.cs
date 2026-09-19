using AlgorithmLab.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=algorithmlab;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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
