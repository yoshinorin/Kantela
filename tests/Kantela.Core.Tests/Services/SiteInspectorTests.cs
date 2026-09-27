using Kantela.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kantela.Core.Tests.Services;

[TestClass]
public sealed class SiteInspectorTests
{
    private const string SiteUrl = "https://a.invalid/";
    private const string FallbackIconUrl = "https://a.invalid/favicon.ico";

    private static readonly byte[] s_png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] s_ico = [0, 0, 1, 0, 1, 0];

    private FakeWebClient _web = null!;
    private SiteInspector _inspector = null!;

    [TestInitialize]
    public void Initialize()
    {
        _web = new FakeWebClient();
        _inspector = new SiteInspector(_web, NullLogger<SiteInspector>.Instance);
    }

    [TestMethod]
    public async Task NewSite_RequiresReachableSite()
    {
        SiteRegistrationException ex = await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("A", SiteUrl, null)));

        Assert.Contains("Could not reach", ex.Message);
    }

    [TestMethod]
    public async Task NewSite_FillsEmptyTitleFromPageOrUrlAndKeepsGivenTitle()
    {
        _web.AddHtml(SiteUrl, "<title>Page title</title>");
        _web.AddHtml("https://b.invalid/", "<p>No title</p>");

        SiteInspection fromPage = await _inspector.InspectAsync(new SiteInput(" ", SiteUrl, null, "Alias"));
        SiteInspection fromUrl = await _inspector.InspectAsync(new SiteInput("", "https://b.invalid/", null));
        SiteInspection given = await _inspector.InspectAsync(new SiteInput("Mine", SiteUrl, null));

        Assert.AreEqual(new SiteInput("Page title", SiteUrl, null, "Alias"), fromPage.Input);
        Assert.IsTrue(fromPage.UrlChanged);
        Assert.AreEqual("https://b.invalid/", fromUrl.Input.Title);
        Assert.AreEqual("Mine", given.Input.Title);
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, "https://a.invalid/feed1")]
    public async Task NewSite_UsesDetectedFeedWhenThereIsAtMostOne(int feedCount, string? expected)
    {
        _web.AddHtml(SiteUrl, FeedLinks(feedCount));

        SiteInspection result = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, null));

        Assert.AreEqual(expected, result.Input.FeedUrl);
    }

    [TestMethod]
    public async Task NewSite_RejectsSeveralDetectedFeedsAndListsThem()
    {
        _web.AddHtml(SiteUrl, FeedLinks(2));

        SiteRegistrationException ex = await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("A", SiteUrl, null)));

        Assert.Contains("https://a.invalid/feed1", ex.Message);
        Assert.Contains("https://a.invalid/feed2", ex.Message);
    }

    [TestMethod]
    public async Task EnteredFeed_IsUsedInsteadOfDetectionAndMustBeAFeed()
    {
        _web.AddHtml(SiteUrl, FeedLinks(2));
        _web.AddFeed("https://a.invalid/entered");
        _web.AddHtml("https://a.invalid/page", "<title>Not a feed</title>");

        SiteInspection result = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/entered"));
        SiteRegistrationException notFeed = await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/page")));
        SiteRegistrationException unreachable = await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/missing")));

        Assert.AreEqual("https://a.invalid/entered", result.Input.FeedUrl);
        Assert.Contains("not an RSS or Atom feed", notFeed.Message);
        Assert.Contains("Could not reach the feed", unreachable.Message);
    }

    [TestMethod]
    public async Task Icon_UsesFirstCandidateThatIsASupportedImage()
    {
        _web.AddHtml(SiteUrl, """
            <link rel="icon" sizes="32x32" href="/broken.png">
            <link rel="icon" sizes="16x16" href="/small.png">
            """);
        _web.AddHtml("https://a.invalid/broken.png", "<html>Not found</html>");
        _web.AddBytes("https://a.invalid/small.png", s_png, "image/png");

        SiteInspection result = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, null));

        Assert.AreEqual("image/png", result.Icon!.ContentType);
        CollectionAssert.AreEqual(s_png, result.Icon.Data);
    }

    [TestMethod]
    public async Task Icon_FallsBackToFaviconIcoAndIsOptional()
    {
        _web.AddHtml(SiteUrl, "<title>A</title>");
        _web.AddHtml("https://b.invalid/", "<title>B</title>");
        _web.AddBytes(FallbackIconUrl, s_ico);

        SiteInspection withIcon = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, null));
        SiteInspection withoutIcon = await _inspector.InspectAsync(new SiteInput("B", "https://b.invalid/", null));

        Assert.AreEqual("image/x-icon", withIcon.Icon!.ContentType);
        Assert.IsNull(withoutIcon.Icon);
    }

    [TestMethod]
    public async Task Icon_RejectsImagesOverTheSizeLimit()
    {
        _web.AddHtml(SiteUrl, "<title>A</title>");
        _web.AddBytes(FallbackIconUrl, [.. s_png, .. new byte[256 * 1024]]);

        SiteInspection result = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, null));

        Assert.IsNull(result.Icon);
    }

    [TestMethod]
    public async Task ExistingSite_RefreshesPageAndIconOnEverySave()
    {
        SiteInput current = new("A", SiteUrl, "https://a.invalid/feed");
        _web.AddHtml(SiteUrl, FeedLinks(2));
        _web.AddBytes(FallbackIconUrl, s_ico);

        SiteInspection result = await _inspector.InspectAsync(new SiteInput("Renamed", SiteUrl, "https://a.invalid/feed"), current);

        // The feed is unchanged, so it is neither detected again nor checked.
        CollectionAssert.AreEqual(new[] { new Uri(SiteUrl), new Uri(FallbackIconUrl) }, _web.Requests);
        Assert.AreEqual("image/x-icon", result.Icon!.ContentType);
        Assert.AreEqual("Renamed", result.Input.Title);
        Assert.AreEqual("https://a.invalid/feed", result.Input.FeedUrl);
        Assert.IsFalse(result.UrlChanged);
    }

    [TestMethod]
    public async Task ExistingSite_CanBeSavedWhileUnreachableUnlessTitleIsEmpty()
    {
        SiteInput current = new("A", SiteUrl, null);

        SiteInspection result = await _inspector.InspectAsync(new SiteInput("Renamed", SiteUrl, null), current);
        await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("", SiteUrl, null), current));

        Assert.AreEqual("Renamed", result.Input.Title);
        Assert.IsNull(result.Icon);
        Assert.IsFalse(result.UrlChanged);
    }

    [TestMethod]
    public async Task ExistingSite_ChecksNewUrlAndNewFeed()
    {
        SiteInput current = new("A", SiteUrl, null);
        _web.AddHtml("https://b.invalid/", FeedLinks(1));
        _web.AddHtml(SiteUrl, FeedLinks(2));
        _web.AddFeed("https://a.invalid/new-feed");

        SiteInspection movedSite = await _inspector.InspectAsync(new SiteInput("A", "https://b.invalid/", null), current);
        SiteInspection newFeed = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/new-feed"), current);
        await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("A", "https://c.invalid/", null), current));

        Assert.AreEqual("https://b.invalid/feed1", movedSite.Input.FeedUrl);
        Assert.IsTrue(movedSite.UrlChanged);
        Assert.AreEqual("https://a.invalid/new-feed", newFeed.Input.FeedUrl);
        Assert.Contains(new Uri("https://a.invalid/new-feed"), _web.Requests);
    }

    private static string FeedLinks(int count) => "<title>A</title>" + string.Concat(
        Enumerable.Range(1, count).Select(i => $"""<link rel="alternate" type="application/rss+xml" href="/feed{i}">"""));
}
