using Kantela.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kantela.Core.Data;

public class KantelaDbContext(DbContextOptions<KantelaDbContext> options) : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();

    public DbSet<SiteIcon> SiteIcons => Set<SiteIcon>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Site>(entity =>
        {
            entity.HasIndex(s => s.Url).IsUnique();
        });

        modelBuilder.Entity<SiteIcon>(entity =>
        {
            entity.HasKey(i => i.SiteId);
            entity.HasOne<Site>()
                .WithOne()
                .HasForeignKey<SiteIcon>(i => i.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // SQLite does not persist DateTimeKind, so values read back are marked as UTC explicitly.
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
