using Microsoft.EntityFrameworkCore;
using LucasWeb.Api.Models;

namespace LucasWeb.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ExecutionDay> ExecutionDays => Set<ExecutionDay>();
    public DbSet<ShiftFeedback> ShiftFeedbacks => Set<ShiftFeedback>();
    public DbSet<WeeklyPrediction> WeeklyPredictions => Set<WeeklyPrediction>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<EstimacionesCache> EstimacionesCaches => Set<EstimacionesCache>();
    public DbSet<AuthToken> AuthTokens => Set<AuthToken>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<FacturacionObjetivoSemana> FacturacionObjetivoSemanas => Set<FacturacionObjetivoSemana>();
    public DbSet<DetectedPattern> DetectedPatterns => Set<DetectedPattern>();
    public DbSet<Models.Event> Events => Set<Models.Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExecutionDay>(e =>
        {
            e.HasIndex(x => x.Date).IsUnique();
            e.Property(x => x.Date).HasConversion(d => d.Date, d => d);
        });

        modelBuilder.Entity<ShiftFeedback>(e =>
        {
            e.HasIndex(x => new { x.ExecutionDayId, x.ShiftName }).IsUnique();
        });

        modelBuilder.Entity<AuthToken>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
        });

        modelBuilder.Entity<EstimacionesCache>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Setting>(e => e.HasKey(x => x.Key));
        modelBuilder.Entity<FacturacionObjetivoSemana>(e =>
        {
            e.HasKey(x => x.WeekStart);
            e.Property(x => x.WeekStart).HasConversion(d => d.Date, d => d);
        });

        modelBuilder.Entity<DetectedPattern>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Models.Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasConversion(d => d.Date, d => d);
            e.HasIndex(x => x.Date);
        });
    }
}
