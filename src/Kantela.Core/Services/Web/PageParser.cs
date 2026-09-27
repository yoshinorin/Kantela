using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;

namespace Kantela.Core.Services.Web;

public sealed record PageMetadata(string? Title, IReadOnlyList<string> FeedUrls);

public static partial class PageParser
{
    private static readonly string[] s_feedTypes = ["application/rss+xml", "application/atom+xml"];

    static PageParser()
    {
        // Legacy encodings such as Shift_JIS are not available on .NET without this provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // Extracts the title and the RSS/Atom feeds advertised by <link rel="alternate">.
    // Relative feed URLs are resolved against the final address of the page.
    public static async Task<PageMetadata> ParseHtmlAsync(WebPage page, CancellationToken cancellationToken = default)
    {
        using IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        using IDocument document = await context.OpenAsync(
            response => response
                .Content(new MemoryStream(page.Content))
                .Address(page.FinalUri)
                .Header(HeaderNames.ContentType, page.ContentType ?? "text/html"),
            cancellationToken);

        string title = Whitespace().Replace(document.Title ?? string.Empty, " ").Trim();
        List<string> feedUrls = document.QuerySelectorAll<IHtmlLinkElement>("link[href]")
            .Where(IsFeedLink)
            .Select(link => link.Href)
            .OfType<string>()
            .Where(UrlValidator.IsWebUrl)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new PageMetadata(title.Length == 0 ? null : title, feedUrls);
    }

    // True when the content is XML whose root element is that of RSS, Atom or RSS 1.0 (RDF).
    public static bool IsFeed(WebPage page)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        try
        {
            using XmlReader reader = XmlReader.Create(new MemoryStream(page.Content), settings);
            reader.MoveToContent();
            return reader.LocalName is "rss" or "feed" or "RDF";
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool IsFeedLink(IHtmlLinkElement link)
    {
        bool isAlternate = (link.Relation ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Contains("alternate", StringComparer.OrdinalIgnoreCase);
        string type = (link.Type ?? string.Empty).Split(';')[0].Trim();
        return isAlternate && s_feedTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
