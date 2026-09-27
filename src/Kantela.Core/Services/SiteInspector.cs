using Kantela.Core.Models;
using Kantela.Core.Services.Web;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services;

// A reason why a site cannot be saved, meant to be shown to the user as is.
public class SiteRegistrationException(string message) : Exception(message);

// Icon is null when no favicon could be fetched. UrlChanged tells whether a previously saved icon belongs to another URL.
public sealed record SiteInspection(SiteInput Input, FaviconImage? Icon, bool UrlChanged);

public sealed class SiteInspector(IWebClient webClient, ILogger<SiteInspector> logger)
{
    private const int MaxIconBytes = 256 * 1024;

    // Checks the site before saving and fills in what the user left empty. The page is fetched on every save:
    // - The site must be reachable when it is new or its URL changed. An existing site whose URL is unchanged
    //   can still be saved while it is unreachable (unless the title is empty), without refreshing anything.
    // - An empty title is taken from the page (or the URL when the page has none).
    // - An empty feed URL is detected from the page when the site is new or its URL changed.
    //   Several candidates are an error, so that the user chooses one; none is fine.
    // - A feed URL entered by the user must point to a feed. It is checked when the site is new or it changed.
    // - The favicon is fetched from the page; failing to get it does not prevent saving.
    // current is the saved state when editing an existing site.
    public async Task<SiteInspection> InspectAsync(
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
        FaviconImage? icon = null;

        WebPage? page = await webClient.GetAsync(new Uri(url), cancellationToken);
        if (page is null)
        {
            if (urlChanged || title.Length == 0)
            {
                throw new SiteRegistrationException($"Could not reach '{url}'. Check the URL and your connection.");
            }

            logger.LogWarning("Could not reach {Url}; saving it without refreshing its icon", url);
        }
        else
        {
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

            icon = await FetchIconAsync(metadata.IconUrls, cancellationToken);
        }

        if (feedUrlEntered)
        {
            await EnsureFeedAsync(feedUrl!, cancellationToken);
        }

        return new SiteInspection(input with { Title = title, Url = url, FeedUrl = feedUrl }, icon, urlChanged);
    }

    // Returns the first candidate that is a supported image of an acceptable size.
    private async Task<FaviconImage?> FetchIconAsync(IReadOnlyList<string> iconUrls, CancellationToken cancellationToken)
    {
        foreach (string iconUrl in iconUrls)
        {
            WebPage? response = await webClient.GetAsync(new Uri(iconUrl), cancellationToken);
            if (response is null)
            {
                continue;
            }

            string? contentType = PageParser.ImageContentType(response.Content);
            if (contentType is null || response.Content.Length > MaxIconBytes)
            {
                logger.LogInformation("Ignored {IconUrl}: not a supported image or too large", iconUrl);
                continue;
            }

            return new FaviconImage(contentType, response.Content);
        }

        return null;
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
