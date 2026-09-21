using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Server.Services;

public class DatabaseConnectionManager
{
    public string ConnectionString { get; private set; } = 
        "Host=localhost;Port=5432;Database=algorithmlab;Username=postgres;Password=postgres";

    public DbConnectionConfig ActiveConfig { get; private set; } = new()
    {
        Host = "localhost",
        Port = 5432,
        Database = "algorithmlab",
        Username = "postgres",
        Password = "postgres"
    };

    public void SetConnection(DbConnectionConfig config)
    {
        ActiveConfig = config ?? throw new ArgumentNullException(nameof(config));
        ConnectionString = config.BuildConnectionString();
    }
}
