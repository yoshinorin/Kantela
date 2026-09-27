using Kantela.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kantela.Core.Tests.Services;

[TestClass]
public sealed class SiteInspectorTests
{
    private const string SiteUrl = "https://a.invalid/";

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

        SiteInput fromPage = await _inspector.InspectAsync(new SiteInput(" ", SiteUrl, null, "Alias"));
        SiteInput fromUrl = await _inspector.InspectAsync(new SiteInput("", "https://b.invalid/", null));
        SiteInput given = await _inspector.InspectAsync(new SiteInput("Mine", SiteUrl, null));

        Assert.AreEqual(new SiteInput("Page title", SiteUrl, null, "Alias"), fromPage);
        Assert.AreEqual("https://b.invalid/", fromUrl.Title);
        Assert.AreEqual("Mine", given.Title);
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, "https://a.invalid/feed1")]
    public async Task NewSite_UsesDetectedFeedWhenThereIsAtMostOne(int feedCount, string? expected)
    {
        _web.AddHtml(SiteUrl, FeedLinks(feedCount));

        SiteInput result = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, null));

        Assert.AreEqual(expected, result.FeedUrl);
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

        SiteInput result = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/entered"));
        SiteRegistrationException notFeed = await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/page")));
        SiteRegistrationException unreachable = await Assert.ThrowsExactlyAsync<SiteRegistrationException>(
            () => _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/missing")));

        Assert.AreEqual("https://a.invalid/entered", result.FeedUrl);
        Assert.Contains("not an RSS or Atom feed", notFeed.Message);
        Assert.Contains("Could not reach the feed", unreachable.Message);
    }

    [TestMethod]
    public async Task ExistingSite_FetchesNothingWhenUrlAndFeedAreUnchangedAndTitleIsGiven()
    {
        SiteInput current = new("A", SiteUrl, "https://a.invalid/feed");

        SiteInput result = await _inspector.InspectAsync(new SiteInput("Renamed", "http://A.invalid", "https://a.invalid/feed"), current);

        Assert.IsEmpty(_web.Requests);
        Assert.AreEqual("Renamed", result.Title);
    }

    [TestMethod]
    public async Task ExistingSite_ChecksNewUrlAndNewFeed()
    {
        SiteInput current = new("A", SiteUrl, null);
        _web.AddHtml("https://b.invalid/", FeedLinks(1));
        _web.AddFeed("https://a.invalid/new-feed");

        SiteInput movedSite = await _inspector.InspectAsync(new SiteInput("A", "https://b.invalid/", null), current);
        SiteInput newFeed = await _inspector.InspectAsync(new SiteInput("A", SiteUrl, "https://a.invalid/new-feed"), current);

        Assert.AreEqual("https://b.invalid/feed1", movedSite.FeedUrl);
        Assert.AreEqual("https://a.invalid/new-feed", newFeed.FeedUrl);
        CollectionAssert.AreEqual(
            new[] { new Uri("https://b.invalid/"), new Uri("https://a.invalid/new-feed") },
            _web.Requests);
    }

    private static string FeedLinks(int count) => "<title>A</title>" + string.Concat(
        Enumerable.Range(1, count).Select(i => $"""<link rel="alternate" type="application/rss+xml" href="/feed{i}">"""));
}
