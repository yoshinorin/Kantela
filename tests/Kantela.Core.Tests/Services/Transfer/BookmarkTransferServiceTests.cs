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
    public async Task Merge_AppendsNewSitesAndSkipsNormalizedDuplicatesAndInvalidUrls()
    {
        await _siteService.AddAsync(new SiteInput("Existing", "https://a.invalid/", null));
        DateTime visitedAt = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime previewedAt = new(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);

        ImportResult result = await _transferService.ImportAsync(
            [
                new ImportedSite("Dup of existing", "http://A.invalid", null),
                new ImportedSite(" B ", " https://b.invalid/ ", "not a url", LastVisitedAt: visitedAt, LastPreviewedAt: previewedAt, Alias: " Bee "),
                new ImportedSite("Dup in file", "https://b.invalid/#top", null),
                new ImportedSite("Invalid", "javascript:alert(1)", null),
                new ImportedSite("", "https://c.invalid/", "https://c.invalid/feed"),
            ],
            ImportMode.Merge);

        Assert.AreEqual(new ImportResult(Added: 2, SkippedDuplicates: 2, SkippedInvalid: 1), result);
        IReadOnlyList<Site> sites = await _siteService.GetAllAsync();
        CollectionAssert.AreEqual(
            new[] { "Existing", "B", "https://c.invalid/" },
            sites.Select(s => s.Title).ToArray());
        Assert.AreEqual("https://b.invalid/", sites[1].Url);
        Assert.IsNull(sites[1].FeedUrl);
        Assert.AreEqual(visitedAt, sites[1].LastVisitedAt);
        Assert.AreEqual(previewedAt, sites[1].LastPreviewedAt);
        Assert.AreEqual("Bee", sites[1].Alias);
        Assert.IsNull(sites[2].Alias);
        Assert.AreEqual(s_now.UtcDateTime, sites[1].CreatedAt);
        Assert.AreEqual("https://c.invalid/feed", sites[2].FeedUrl);
    }

    [TestMethod]
    public async Task Replace_DeletesExistingSitesAfterBackingThemUp()
    {
        await _siteService.AddAsync(new SiteInput("Old", "https://old.invalid/", null));

        ImportResult result = await _transferService.ImportAsync(
            [new ImportedSite("New", "https://new.invalid/", null)],
            ImportMode.Replace);

        Assert.AreEqual(new ImportResult(1, 0, 0), result);
        Assert.AreEqual("New", (await _siteService.GetAllAsync()).Single().Title);
        string backup = Directory.GetFiles(BackupDirectory).Single();
        await using FileStream stream = File.OpenRead(backup);
        Assert.AreEqual("https://old.invalid/", JsonBookmarkFormat.Read(stream).Single().Url);
    }

    [TestMethod]
    [DataRow(BookmarkFormat.Json, 2, 0)]
    [DataRow(BookmarkFormat.Opml, 1, 1)]
    public async Task ExportThenReplaceImport_RestoresExportedSites(BookmarkFormat format, int expectedExported, int expectedSkipped)
    {
        await _siteService.AddAsync(new SiteInput("A", "https://a.invalid/", "https://a.invalid/feed"));
        await _siteService.AddAsync(new SiteInput("B", "https://b.invalid/", null));
        string path = Path.Combine(_directory.Path, "export" + format.DefaultExtension());

        ExportResult exportResult = await _transferService.ExportAsync(format, path);
        await _siteService.AddAsync(new SiteInput("C", "https://c.invalid/", null));
        await _transferService.ImportAsync(format, path, ImportMode.Replace);

        Assert.AreEqual(new ExportResult(expectedExported, expectedSkipped), exportResult);
        CollectionAssert.AreEqual(
            new[] { "A", "B" }.Take(expectedExported).ToArray(),
            (await _siteService.GetAllAsync()).Select(s => s.Title).ToArray());
    }
}
