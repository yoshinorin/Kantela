using Kantela.Core.Models;
using Kantela.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kantela.Core.Tests.Services;

[TestClass]
public sealed class SiteServiceTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    private TestDatabase _database = null!;
    private FixedTimeProvider _timeProvider = null!;
    private SiteService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        _database = new TestDatabase();
        _timeProvider = new FixedTimeProvider(s_now);
        _service = new SiteService(_database.Factory, _timeProvider, NullLogger<SiteService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _database.Dispose();

    [TestMethod]
    public async Task AddAsync_NormalizesInputAndAppendsToEnd()
    {
        await _service.AddAsync(new SiteInput("First", "https://a.example/", null));

        Site site = await _service.AddAsync(new SiteInput("  Second ", " https://b.example/ ", "  "));

        Assert.AreEqual("Second", site.Title);
        Assert.AreEqual("https://b.example/", site.Url);
        Assert.IsNull(site.FeedUrl);
        Assert.AreEqual(1, site.SortOrder);
        Assert.AreEqual(s_now.UtcDateTime, site.CreatedAt);
    }

    [TestMethod]
    [DataRow("", "https://a.example/", null)]
    [DataRow("Title", "not a url", null)]
    [DataRow("Title", "ftp://a.example/", null)]
    [DataRow("Title", "https://a.example/", "not a url")]
    public async Task AddAsync_RejectsInvalidInput(string title, string url, string? feedUrl)
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.AddAsync(new SiteInput(title, url, feedUrl)));
    }

    [TestMethod]
    public async Task AddAsync_RejectsDuplicateUrl()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.example/", null));

        DuplicateSiteUrlException ex = await Assert.ThrowsExactlyAsync<DuplicateSiteUrlException>(
            () => _service.AddAsync(new SiteInput("B", " https://a.example/", null)));
        Assert.AreEqual("https://a.example/", ex.Url);
    }

    [TestMethod]
    public async Task UpdateAsync_UpdatesFieldsAndAllowsKeepingOwnUrl()
    {
        Site site = await _service.AddAsync(new SiteInput("A", "https://a.example/", null));

        await _service.UpdateAsync(site.Id, new SiteInput("A2", "https://a.example/", "https://a.example/feed"));

        Site updated = (await _service.GetAllAsync()).Single();
        Assert.AreEqual("A2", updated.Title);
        Assert.AreEqual("https://a.example/feed", updated.FeedUrl);
    }

    [TestMethod]
    public async Task UpdateAsync_RejectsUrlOfAnotherSite()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
        Site b = await _service.AddAsync(new SiteInput("B", "https://b.example/", null));

        await Assert.ThrowsExactlyAsync<DuplicateSiteUrlException>(
            () => _service.UpdateAsync(b.Id, new SiteInput("B", "https://a.example/", null)));
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesSite()
    {
        Site a = await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
        await _service.AddAsync(new SiteInput("B", "https://b.example/", null));

        await _service.DeleteAsync(a.Id);

        Assert.AreEqual("B", (await _service.GetAllAsync()).Single().Title);
    }

    [TestMethod]
    public async Task MarkVisitedAsync_StoresCurrentTime()
    {
        Site site = await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
        _timeProvider.Now = s_now.AddDays(1);

        DateTime visitedAt = await _service.MarkVisitedAsync(site.Id);

        Assert.AreEqual(s_now.AddDays(1).UtcDateTime, visitedAt);
        Assert.AreEqual(visitedAt, (await _service.GetAllAsync()).Single().LastVisitedAt);
    }

    [TestMethod]
    public async Task MarkPreviewedAsync_StoresCurrentTimeWithoutChangingVisit()
    {
        Site site = await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
        _timeProvider.Now = s_now.AddDays(1);

        DateTime previewedAt = await _service.MarkPreviewedAsync(site.Id);

        Assert.AreEqual(s_now.AddDays(1).UtcDateTime, previewedAt);
        Site stored = (await _service.GetAllAsync()).Single();
        Assert.AreEqual(previewedAt, stored.LastPreviewedAt);
        Assert.IsNull(stored.LastVisitedAt);
    }

    [TestMethod]
    public async Task ReorderAsync_AppliesOrderAndKeepsUnlistedSitesAtEnd()
    {
        Site a = await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
        Site b = await _service.AddAsync(new SiteInput("B", "https://b.example/", null));
        Site c = await _service.AddAsync(new SiteInput("C", "https://c.example/", null));

        await _service.ReorderAsync([c.Id, a.Id]);

        IReadOnlyList<Site> sites = await _service.GetAllAsync();
        CollectionAssert.AreEqual(new[] { "C", "A", "B" }, sites.Select(s => s.Title).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, sites.Select(s => s.SortOrder).ToArray());
    }
}
