using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;

namespace Kantela.Core.Services.Web;

// IconUrls are the favicon URLs to try in order.
public sealed record PageMetadata(string? Title, IReadOnlyList<string> FeedUrls, IReadOnlyList<string> IconUrls);

public static partial class PageParser
{
    private static readonly string[] s_feedTypes = ["application/rss+xml", "application/atom+xml"];
    private static readonly string[] s_iconRels = ["icon", "apple-touch-icon", "apple-touch-icon-precomposed"];

    // Icons declared by the page that are tried before /favicon.ico, to bound the number of requests.
    private const int MaxDeclaredIcons = 2;

    static PageParser()
    {
        // Legacy encodings such as Shift_JIS are not available on .NET without this provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // Extracts the title, the RSS/Atom feeds advertised by <link rel="alternate"> and the favicon candidates.
    // Relative URLs are resolved against the final address of the page.
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
        List<IHtmlLinkElement> links = [.. document.QuerySelectorAll<IHtmlLinkElement>("link[href]")];
        List<string> feedUrls = WebUrls(links.Where(IsFeedLink)).ToList();

        // Declared icons first (best first), then the conventional /favicon.ico of the host.
        List<string> iconUrls = WebUrls(links.Where(IsIconLink).OrderBy(IconRank))
            .Take(MaxDeclaredIcons)
            .Append(new Uri(page.FinalUri, "/favicon.ico").AbsoluteUri)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new PageMetadata(title.Length == 0 ? null : title, feedUrls, iconUrls);
    }

    // The media type of a supported favicon image, detected from its content rather than the (often wrong)
    // Content-Type header; null for anything else, such as an HTML error page.
    public static string? ImageContentType(byte[] data)
    {
        ReadOnlySpan<byte> bytes = data;
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']))
        {
            return "image/png";
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0, 0, 1, 0]))
        {
            return "image/x-icon";
        }

        if (bytes.StartsWith("GIF8"u8))
        {
            return "image/gif";
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (bytes.StartsWith("BM"u8))
        {
            return "image/bmp";
        }

        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        string head = Encoding.UTF8.GetString(bytes[..Math.Min(bytes.Length, 1024)]);
        return head.Contains("<svg", StringComparison.OrdinalIgnoreCase) && !head.Contains("<html", StringComparison.OrdinalIgnoreCase)
            ? "image/svg+xml"
            : null;
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

    private static bool IsFeedLink(IHtmlLinkElement link) =>
        HasRel(link, "alternate") && s_feedTypes.Contains(MediaType(link), StringComparer.OrdinalIgnoreCase);

    private static bool IsIconLink(IHtmlLinkElement link) => s_iconRels.Any(rel => HasRel(link, rel));

    // Lower is better: SVG (scales to any size), then the declared size closest to the 32 px used for display.
    // Apple touch icons are large images meant for home screens, so they come after regular icons.
    private static int IconRank(IHtmlLinkElement link)
    {
        int penalty = HasRel(link, "icon") ? 0 : 1000;
        string sizes = link.GetAttribute("sizes") ?? string.Empty;
        if (MediaType(link).Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase)
            || sizes.Equals("any", StringComparison.OrdinalIgnoreCase)
            || (link.Href ?? string.Empty).EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return penalty;
        }

        int[] declared = [.. IconSize().Matches(sizes).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))];
        int size = declared.Length == 0 ? 16 : declared.MinBy(s => Math.Abs(s - 32));
        return penalty + 1 + Math.Abs(size - 32);
    }

    private static bool HasRel(IHtmlLinkElement link, string rel) =>
        (link.Relation ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Contains(rel, StringComparer.OrdinalIgnoreCase);

    private static string MediaType(IHtmlLinkElement link) => (link.Type ?? string.Empty).Split(';')[0].Trim();

    private static IEnumerable<string> WebUrls(IEnumerable<IHtmlLinkElement> links) =>
        links.Select(link => link.Href).OfType<string>().Where(UrlValidator.IsWebUrl).Distinct(StringComparer.Ordinal);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(\d+)[xX]\d+")]
    private static partial Regex IconSize();
}
