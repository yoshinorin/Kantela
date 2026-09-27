using System.Text;
using Kantela.Core.Models;
using Kantela.Core.Services.Transfer;

namespace Kantela.Core.Tests.Services.Transfer;

[TestClass]
public sealed class JsonBookmarkFormatTests
{
    [TestMethod]
    public void WriteThenRead_PreservesFieldsAndOrder()
    {
        DateTime createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime visitedAt = new(2026, 2, 1, 12, 30, 0, DateTimeKind.Utc);
        DateTime previewedAt = new(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        List<Site> sites =
        [
            new() { Title = "B", Url = "https://b.invalid/", CreatedAt = createdAt },
            new() { Title = "A", Url = "https://a.invalid/", FeedUrl = "https://a.invalid/feed", CreatedAt = createdAt, LastVisitedAt = visitedAt, LastPreviewedAt = previewedAt, Alias = "Alias" },
        ];
        using MemoryStream stream = new();

        JsonBookmarkFormat.Write(stream, sites, createdAt);
        stream.Position = 0;
        IReadOnlyList<ImportedSite> imported = JsonBookmarkFormat.Read(stream);

        CollectionAssert.AreEqual(
            new[]
            {
                new ImportedSite("B", "https://b.invalid/", null, createdAt, null),
                new ImportedSite("A", "https://a.invalid/", "https://a.invalid/feed", createdAt, visitedAt, previewedAt, "Alias"),
            },
            imported.ToArray());
        Assert.AreEqual(DateTimeKind.Utc, imported[1].LastVisitedAt!.Value.Kind);
    }

    [TestMethod]
    public void Read_AcceptsOlderDocumentsAndKeepsFileOrder()
    {
        // Older exports have "sortOrder" and no "lastPreviewedAt" or "alias".
        string json = """
            { "version": 1, "exportedAt": "2026-01-01T00:00:00Z", "sites": [
              { "title": "B", "url": "https://b.invalid/", "feedUrl": null, "sortOrder": 1, "createdAt": "2026-01-01T00:00:00Z", "lastVisitedAt": null },
              { "title": "A", "url": "https://a.invalid/", "feedUrl": null, "sortOrder": 0, "createdAt": "2026-01-01T00:00:00Z", "lastVisitedAt": null } ] }
            """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));

        IReadOnlyList<ImportedSite> imported = JsonBookmarkFormat.Read(stream);

        CollectionAssert.AreEqual(new[] { "B", "A" }, imported.Select(s => s.Title).ToArray());
        Assert.IsTrue(imported.All(s => s.LastPreviewedAt is null && s.Alias is null));
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("""{ "version": 2, "exportedAt": "2026-01-01T00:00:00Z", "sites": [] }""")]
    [DataRow("""{ "version": 1, "exportedAt": "2026-01-01T00:00:00Z", "sites": [ { "title": "A", "createdAt": "2026-01-01T00:00:00Z" } ] }""")]
    public void Read_RejectsInvalidDocuments(string json)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));

        Assert.ThrowsExactly<InvalidDataException>(() => JsonBookmarkFormat.Read(stream));
    }
}
