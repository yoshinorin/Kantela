using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Kantela.Core.Models;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services.Transfer;

public static class OpmlBookmarkFormat
{
    public static int Write(Stream stream, IReadOnlyList<Site> sites, DateTime exportedAt, ILogger logger)
    {
        List<XElement> outlines = [];
        int skipped = 0;
        foreach (Site site in sites)
        {
            if (site.FeedUrl is null)
            {
                logger.LogWarning("Site {Url} was not exported to OPML because it has no feed URL", site.Url);
                skipped++;
                continue;
            }

            outlines.Add(new XElement("outline",
                new XAttribute("type", "rss"),
                new XAttribute("text", site.Title),
                new XAttribute("title", site.Title),
                new XAttribute("xmlUrl", site.FeedUrl),
                new XAttribute("htmlUrl", site.Url)));
        }

        XDocument document = new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("opml",
                new XAttribute("version", "2.0"),
                new XElement("head",
                    new XElement("title", "Kantela"),
                    new XElement("dateCreated", exportedAt.ToString("r", CultureInfo.InvariantCulture))),
                new XElement("body", outlines)));
        document.Save(stream);
        return skipped;
    }

    public static IReadOnlyList<ImportedSite> Read(Stream stream, ILogger logger)
    {
        XDocument document;
        try
        {
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"The file is not a valid OPML document: {ex.Message}", ex);
        }

        XElement body = document.Root?.Name == "opml"
            ? document.Root.Element("body") ?? throw new InvalidDataException("The OPML document has no body.")
            : throw new InvalidDataException("The file is not an OPML document.");

        List<ImportedSite> sites = [];
        foreach (XElement outline in body.Descendants("outline"))
        {
            string? feedUrl = (string?)outline.Attribute("xmlUrl");
            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                // Outlines with children are groups (folders in other readers) and are flattened.
                if (!outline.HasElements)
                {
                    logger.LogWarning("Skipped OPML outline {Text} because it has no xmlUrl", (string?)outline.Attribute("text"));
                }

                continue;
            }

            string? htmlUrl = (string?)outline.Attribute("htmlUrl");
            if (string.IsNullOrWhiteSpace(htmlUrl))
            {
                logger.LogWarning("OPML outline for {FeedUrl} has no htmlUrl; the feed URL is used as the site URL", feedUrl);
                htmlUrl = feedUrl;
            }

            string title = (string?)outline.Attribute("text") ?? (string?)outline.Attribute("title") ?? string.Empty;
            sites.Add(new ImportedSite(title, htmlUrl, feedUrl));
        }

        return sites;
    }
}
