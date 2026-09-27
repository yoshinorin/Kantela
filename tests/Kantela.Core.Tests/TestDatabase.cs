using Kantela.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kantela.Core.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Factory = new KantelaDbContextFactory(
            new DbContextOptionsBuilder<KantelaDbContext>().UseSqlite(_connection).Options);

        using KantelaDbContext db = Factory.CreateDbContext();
        db.Database.Migrate();
    }

    public KantelaDbContextFactory Factory { get; }

    public void Dispose() => _connection.Dispose();
}
