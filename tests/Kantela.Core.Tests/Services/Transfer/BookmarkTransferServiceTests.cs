using Kantela.Core.Models;
using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kantela.Core.Tests.Services.Transfer;

[TestClass]
public sealed class BookmarkTransferServiceTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    private TestDatabase _database = null!;
    private TestDirectory _directory = null!;
    private SiteService _siteService = null!;
    private BookmarkTransferService _transferService = null!;

    private string BackupDirectory => Path.Combine(_directory.Path, "backups");

    [TestInitialize]
    public void Initialize()
    {
        _database = new TestDatabase();
        _directory = new TestDirectory();
        FixedTimeProvider timeProvider = new(s_now);
        _siteService = new SiteService(_database.Factory, timeProvider, NullLogger<SiteService>.Instance);
        _transferService = new BookmarkTransferService(
            _database.Factory,
            new BackupService(_database.Factory, BackupDirectory, timeProvider, NullLogger<BackupService>.Instance),
            timeProvider,
            NullLogger<BookmarkTransferService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _database.Dispose();
        _directory.Dispose();
    }

    [TestMethod]
    public async Task Merge_AppendsNewSitesAndSkipsDuplicatesAndInvalidUrls()
    {
        await _siteService.AddAsync(new SiteInput("Existing", "https://a.example/", null));
        DateTime visitedAt = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        ImportResult result = await _transferService.ImportAsync(
            [
                new ImportedSite("Dup of existing", "https://a.example/", null),
                new ImportedSite(" B ", " https://b.example/ ", "not a url", LastVisitedAt: visitedAt),
                new ImportedSite("Dup in file", "https://b.example/", null),
                new ImportedSite("Invalid", "javascript:alert(1)", null),
                new ImportedSite("", "https://c.example/", "https://c.example/feed"),
            ],
            ImportMode.Merge);

        Assert.AreEqual(new ImportResult(Added: 2, SkippedDuplicates: 2, SkippedInvalid: 1), result);
        IReadOnlyList<Site> sites = await _siteService.GetAllAsync();
        CollectionAssert.AreEqual(
            new[] { "Existing", "B", "https://c.example/" },
            sites.Select(s => s.Title).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, sites.Select(s => s.SortOrder).ToArray());
        Assert.AreEqual("https://b.example/", sites[1].Url);
        Assert.IsNull(sites[1].FeedUrl);
        Assert.AreEqual(visitedAt, sites[1].LastVisitedAt);
        Assert.AreEqual(s_now.UtcDateTime, sites[1].CreatedAt);
        Assert.AreEqual("https://c.example/feed", sites[2].FeedUrl);
    }

    [TestMethod]
    public async Task Replace_DeletesExistingSitesAfterBackingThemUp()
    {
        await _siteService.AddAsync(new SiteInput("Old", "https://old.example/", null));

        ImportResult result = await _transferService.ImportAsync(
            [new ImportedSite("New", "https://new.example/", null)],
            ImportMode.Replace);

        Assert.AreEqual(new ImportResult(1, 0, 0), result);
        Assert.AreEqual("New", (await _siteService.GetAllAsync()).Single().Title);
        string backup = Directory.GetFiles(BackupDirectory).Single();
        await using FileStream stream = File.OpenRead(backup);
        Assert.AreEqual("https://old.example/", JsonBookmarkFormat.Read(stream).Single().Url);
    }

    [TestMethod]
    [DataRow(BookmarkFormat.Json, 2, 0)]
    [DataRow(BookmarkFormat.Opml, 1, 1)]
    public async Task ExportThenReplaceImport_RestoresExportedSites(BookmarkFormat format, int expectedExported, int expectedSkipped)
    {
        await _siteService.AddAsync(new SiteInput("A", "https://a.example/", "https://a.example/feed"));
        await _siteService.AddAsync(new SiteInput("B", "https://b.example/", null));
        string path = Path.Combine(_directory.Path, "export" + format.DefaultExtension());

        ExportResult exportResult = await _transferService.ExportAsync(format, path);
        await _siteService.AddAsync(new SiteInput("C", "https://c.example/", null));
        await _transferService.ImportAsync(format, path, ImportMode.Replace);

        Assert.AreEqual(new ExportResult(expectedExported, expectedSkipped), exportResult);
        CollectionAssert.AreEqual(
            new[] { "A", "B" }.Take(expectedExported).ToArray(),
            (await _siteService.GetAllAsync()).Select(s => s.Title).ToArray());
    }
}
