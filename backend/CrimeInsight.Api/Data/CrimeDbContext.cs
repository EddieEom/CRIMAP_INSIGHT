using CrimeInsight.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrimeInsight.Api.Data;

public sealed class CrimeDbContext(DbContextOptions<CrimeDbContext> options) : DbContext(options)
{
    public DbSet<CrimeObservation> Observations => Set<CrimeObservation>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var obs = builder.Entity<CrimeObservation>();
        obs.Property(x => x.Region).HasMaxLength(100).IsRequired();
        obs.Property(x => x.Province).HasMaxLength(20).IsRequired();
        obs.Property(x => x.District).HasMaxLength(90).IsRequired();
        obs.Property(x => x.CategoryMajor).HasMaxLength(80).IsRequired();
        obs.Property(x => x.CategoryMinor).HasMaxLength(80).IsRequired();
        var run = builder.Entity<IngestionRun>();
        run.Property(x => x.Status).HasMaxLength(20);
        run.Property(x => x.Sha256).HasMaxLength(64);
        run.Property(x => x.SourceEndpoint).HasMaxLength(180);
        run.Property(x => x.ErrorSummary).HasMaxLength(1800);
    }
}
