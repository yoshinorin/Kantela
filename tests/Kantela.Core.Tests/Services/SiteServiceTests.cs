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
        await _service.AddAsync(new SiteInput("First", "https://a.invalid/", null));

        Site site = await _service.AddAsync(new SiteInput("  Second ", " https://b.invalid/ ", "  ", " Mine "));

        Assert.AreEqual("Second", site.Title);
        Assert.AreEqual("Mine", site.Alias);
        Assert.AreEqual("https://b.invalid/", site.Url);
        Assert.IsNull(site.FeedUrl);
        Assert.AreEqual(s_now.UtcDateTime, site.CreatedAt);
        CollectionAssert.AreEqual(new[] { "First", "Second" }, (await _service.GetAllAsync()).Select(s => s.Title).ToArray());
    }

    [TestMethod]
    [DataRow("", "https://a.invalid/", null)]
    [DataRow("Title", "not a url", null)]
    [DataRow("Title", "ftp://a.invalid/", null)]
    [DataRow("Title", "https://a.invalid/", "not a url")]
    public async Task AddAsync_RejectsInvalidInput(string title, string url, string? feedUrl)
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.AddAsync(new SiteInput(title, url, feedUrl)));
    }

    [TestMethod]
    public async Task AddAsync_RejectsDuplicateUrlAfterNormalization()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));

        DuplicateSiteUrlException ex = await Assert.ThrowsExactlyAsync<DuplicateSiteUrlException>(
            () => _service.AddAsync(new SiteInput("B", " http://A.invalid", null)));
        Assert.AreEqual("http://A.invalid", ex.Url);
    }

    [TestMethod]
    public async Task UpdateAsync_UpdatesFieldsAndAllowsKeepingOwnUrl()
    {
        Site site = await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));

        await _service.UpdateAsync(site.Id, new SiteInput("A2", "https://a.invalid/", "https://a.invalid/feed"));

        Site updated = (await _service.GetAllAsync()).Single();
        Assert.AreEqual("A2", updated.Title);
        Assert.AreEqual("https://a.invalid/feed", updated.FeedUrl);
    }

    [TestMethod]
    public async Task UpdateAsync_RejectsUrlOfAnotherSite()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));
        Site b = await _service.AddAsync(new SiteInput("B", "https://b.invalid/", null));

        await Assert.ThrowsExactlyAsync<DuplicateSiteUrlException>(
            () => _service.UpdateAsync(b.Id, new SiteInput("B", "https://a.invalid/", null)));
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesSite()
    {
        Site a = await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));
        await _service.AddAsync(new SiteInput("B", "https://b.invalid/", null));

        await _service.DeleteAsync(a.Id);

        Assert.AreEqual("B", (await _service.GetAllAsync()).Single().Title);
    }

    [TestMethod]
    public async Task MarkVisitedAsync_StoresCurrentTime()
    {
        Site site = await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));
        _timeProvider.Now = s_now.AddDays(1);

        DateTime visitedAt = await _service.MarkVisitedAsync(site.Id);

        Assert.AreEqual(s_now.AddDays(1).UtcDateTime, visitedAt);
        Assert.AreEqual(visitedAt, (await _service.GetAllAsync()).Single().LastVisitedAt);
    }

    [TestMethod]
    public async Task MarkPreviewedAsync_StoresCurrentTimeWithoutChangingVisit()
    {
        Site site = await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));
        _timeProvider.Now = s_now.AddDays(1);

        DateTime previewedAt = await _service.MarkPreviewedAsync(site.Id);

        Assert.AreEqual(s_now.AddDays(1).UtcDateTime, previewedAt);
        Site stored = (await _service.GetAllAsync()).Single();
        Assert.AreEqual(previewedAt, stored.LastPreviewedAt);
        Assert.IsNull(stored.LastVisitedAt);
    }
}
