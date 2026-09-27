using Kantela.Core.Services.Web;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services;

// A reason why a site cannot be saved, meant to be shown to the user as is.
public class SiteRegistrationException(string message) : Exception(message);

public sealed class SiteInspector(IWebClient webClient, ILogger<SiteInspector> logger)
{
    // Checks the site before saving and fills in what the user left empty:
    // - The site must be reachable. Its page is fetched when the site is new, its URL changed, or the title is empty.
    // - An empty title is taken from the page (or the URL when the page has none).
    // - An empty feed URL is detected from the page when the site is new or its URL changed.
    //   Several candidates are an error, so that the user chooses one; none is fine.
    // - A feed URL entered by the user must point to a feed. It is checked when the site is new or it changed.
    // current is the saved state when editing an existing site.
    public async Task<SiteInput> InspectAsync(
        SiteInput input, SiteInput? current = null, CancellationToken cancellationToken = default)
    {
        string url = input.Url.Trim();
        string title = input.Title.Trim();
        string? feedUrl = Blank(input.FeedUrl);
        if (!UrlValidator.IsWebUrl(url))
        {
            throw new SiteRegistrationException($"'{url}' is not a valid web URL.");
        }

        bool urlChanged = current is null || UrlNormalizer.ComparisonKey(url) != UrlNormalizer.ComparisonKey(current.Url);
        bool feedUrlEntered = feedUrl is not null && (current is null || feedUrl != Blank(current.FeedUrl));

        if (urlChanged || title.Length == 0)
        {
            WebPage page = await webClient.GetAsync(new Uri(url), cancellationToken)
                ?? throw new SiteRegistrationException($"Could not reach '{url}'. Check the URL and your connection.");
            PageMetadata metadata = await PageParser.ParseHtmlAsync(page, cancellationToken);

            if (title.Length == 0)
            {
                title = metadata.Title ?? url;
            }

            if (feedUrl is null && urlChanged)
            {
                if (metadata.FeedUrls.Count > 1)
                {
                    throw new SiteRegistrationException(
                        "Several feeds were found. Enter the one to use in Feed URL and save again:\n"
                        + string.Join("\n", metadata.FeedUrls.Select(f => $"- {f}")));
                }

                feedUrl = metadata.FeedUrls.FirstOrDefault();
                if (feedUrl is not null)
                {
                    logger.LogInformation("Detected feed {FeedUrl} for {Url}", feedUrl, url);
                }
            }
        }

        if (feedUrlEntered)
        {
            await EnsureFeedAsync(feedUrl!, cancellationToken);
        }

        return input with { Title = title, Url = url, FeedUrl = feedUrl };
    }

    private async Task EnsureFeedAsync(string feedUrl, CancellationToken cancellationToken)
    {
        if (!UrlValidator.IsWebUrl(feedUrl))
        {
            throw new SiteRegistrationException($"'{feedUrl}' is not a valid web URL.");
        }

        WebPage feed = await webClient.GetAsync(new Uri(feedUrl), cancellationToken)
            ?? throw new SiteRegistrationException($"Could not reach the feed '{feedUrl}'.");
        if (!PageParser.IsFeed(feed))
        {
            throw new SiteRegistrationException($"'{feedUrl}' is not an RSS or Atom feed.");
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
