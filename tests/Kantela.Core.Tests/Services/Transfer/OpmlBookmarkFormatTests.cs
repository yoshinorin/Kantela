using System.Text;
using System.Xml.Linq;
using Kantela.Core.Models;
using Kantela.Core.Services.Transfer;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Tests.Services.Transfer;

[TestClass]
public sealed class OpmlBookmarkFormatTests
{
    [TestMethod]
    public void Write_ExportsSitesWithFeedAndWarnsForOthers()
    {
        List<Site> sites =
        [
            new() { Title = "A", Url = "https://a.invalid/", FeedUrl = "https://a.invalid/feed" },
            new() { Title = "B", Url = "https://b.invalid/" },
        ];
        ListLogger logger = new();
        using MemoryStream stream = new();

        int skipped = OpmlBookmarkFormat.Write(stream, sites, DateTime.UtcNow, logger);

        Assert.AreEqual(1, skipped);
        stream.Position = 0;
        XElement outline = XDocument.Load(stream).Root!.Element("body")!.Elements("outline").Single();
        Assert.AreEqual("rss", (string?)outline.Attribute("type"));
        Assert.AreEqual("A", (string?)outline.Attribute("text"));
        Assert.AreEqual("https://a.invalid/feed", (string?)outline.Attribute("xmlUrl"));
        Assert.AreEqual("https://a.invalid/", (string?)outline.Attribute("htmlUrl"));
        (LogLevel level, string message) = logger.Entries.Single();
        Assert.AreEqual(LogLevel.Warning, level);
        Assert.Contains("https://b.invalid/", message);
    }

    [TestMethod]
    public void Read_FlattensGroupsAndFallsBackToFeedUrl()
    {
        const string opml = """
            <?xml version="1.0" encoding="utf-8"?>
            <opml version="2.0">
              <head><title>Other reader</title></head>
              <body>
                <outline text="Group">
                  <outline type="rss" text="A" xmlUrl="https://a.invalid/feed" htmlUrl="https://a.invalid/" />
                  <outline text="Nested">
                    <outline type="rss" title="B" xmlUrl="https://b.invalid/feed" />
                  </outline>
                </outline>
                <outline text="No feed" />
                <outline type="rss" text="C" xmlUrl="https://c.invalid/feed" htmlUrl="https://c.invalid/" />
              </body>
            </opml>
            """;
        ListLogger logger = new();
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(opml));

        IReadOnlyList<ImportedSite> sites = OpmlBookmarkFormat.Read(stream, logger);

        CollectionAssert.AreEqual(
            new[]
            {
                new ImportedSite("A", "https://a.invalid/", "https://a.invalid/feed"),
                new ImportedSite("B", "https://b.invalid/feed", "https://b.invalid/feed"),
                new ImportedSite("C", "https://c.invalid/", "https://c.invalid/feed"),
            },
            sites.ToArray());
        Assert.AreEqual(2, logger.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [TestMethod]
    [DataRow("not xml")]
    [DataRow("<rss />")]
    [DataRow("<opml version=\"2.0\"><head /></opml>")]
    public void Read_RejectsInvalidDocuments(string content)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(content));

        Assert.ThrowsExactly<InvalidDataException>(() => OpmlBookmarkFormat.Read(stream, new ListLogger()));
    }
}
