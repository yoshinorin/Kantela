using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kantela.Core.Data;

public sealed class KantelaDbContextFactory(DbContextOptions<KantelaDbContext> options)
    : IDbContextFactory<KantelaDbContext>
{
    public KantelaDbContext CreateDbContext() => new(options);

    public static KantelaDbContextFactory ForFile(string databasePath)
    {
        // Foreign keys are required so that deleting a site also deletes its icon.
        string connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();
        return new KantelaDbContextFactory(
            new DbContextOptionsBuilder<KantelaDbContext>().UseSqlite(connectionString).Options);
    }
}

// Used only by `dotnet ef` at design time.
internal sealed class DesignTimeKantelaDbContextFactory : IDesignTimeDbContextFactory<KantelaDbContext>
{
    public KantelaDbContext CreateDbContext(string[] args) =>
        KantelaDbContextFactory.ForFile("design-time.db").CreateDbContext();
}
