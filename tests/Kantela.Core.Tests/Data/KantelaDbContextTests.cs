using Kantela.Core.Data;
using Kantela.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Kantela.Core.Tests.Data;

[TestClass]
public sealed class KantelaDbContextTests
{
    [TestMethod]
    public void DateTimesAreReadBackAsUtc()
    {
        using TestDatabase database = new();
        DateTime createdAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        DateTime lastVisitedAt = new(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);

        using (KantelaDbContext db = database.Factory.CreateDbContext())
        {
            db.Sites.Add(new Site
            {
                Title = "Example",
                Url = "https://example.com/",
                CreatedAt = createdAt,
                LastVisitedAt = lastVisitedAt,
            });
            db.SaveChanges();
        }

        using (KantelaDbContext db = database.Factory.CreateDbContext())
        {
            Site site = db.Sites.Single();
            Assert.AreEqual(createdAt, site.CreatedAt);
            Assert.AreEqual(DateTimeKind.Utc, site.CreatedAt.Kind);
            Assert.AreEqual(lastVisitedAt, site.LastVisitedAt);
            Assert.AreEqual(DateTimeKind.Utc, site.LastVisitedAt!.Value.Kind);
        }
    }

    [TestMethod]
    public void UrlMustBeUnique()
    {
        using TestDatabase database = new();
        using KantelaDbContext db = database.Factory.CreateDbContext();
        db.Sites.Add(new Site { Title = "A", Url = "https://example.com/" });
        db.Sites.Add(new Site { Title = "B", Url = "https://example.com/" });

        Assert.ThrowsExactly<DbUpdateException>(() => db.SaveChanges());
    }
}
