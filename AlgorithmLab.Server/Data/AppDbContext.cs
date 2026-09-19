using Microsoft.EntityFrameworkCore;

namespace AlgorithmLab.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ExperimentEntity> Experiments => Set<ExperimentEntity>();
    public DbSet<ExperimentPointEntity> ExperimentPoints => Set<ExperimentPointEntity>();
    public DbSet<PointRunEntity> PointRuns => Set<PointRunEntity>();
    public DbSet<BenchmarkCacheEntity> BenchmarkCache => Set<BenchmarkCacheEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BenchmarkCacheEntity>()
            .HasKey(c => new { c.AlgorithmId, c.N, c.M, c.ConfigHash });

        modelBuilder.Entity<ExperimentEntity>()
            .HasMany(e => e.Points)
            .WithOne(p => p.Experiment)
            .HasForeignKey(p => p.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExperimentPointEntity>()
            .HasMany(p => p.Runs)
            .WithOne(r => r.Point)
            .HasForeignKey(r => r.PointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
