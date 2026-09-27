using System.Text;
using Kantela.Core.Services.Web;

namespace Kantela.Core.Tests.Services.Web;

[TestClass]
public sealed class PageParserTests
{
    [TestMethod]
    public async Task ParseHtmlAsync_ExtractsTitleAndAlternateFeeds()
    {
        WebPage page = Html("""
            <html><head>
              <title>
                Site &amp;
                Blog
              </title>
              <link rel="alternate" type="application/rss+xml" href="/feed.xml">
              <link rel="Alternate" type="application/atom+xml; charset=utf-8" href="https://feeds.invalid/atom">
              <link rel="alternate" type="application/rss+xml" href="feed.xml">
              <link rel="alternate" type="text/html" href="/en/">
              <link rel="stylesheet" type="application/rss+xml" href="/not-a-feed.xml">
              <link rel="alternate" type="application/rss+xml" href="javascript:alert(1)">
            </head></html>
            """, finalUrl: "https://a.invalid/blog/");

        PageMetadata metadata = await PageParser.ParseHtmlAsync(page);

        Assert.AreEqual("Site & Blog", metadata.Title);
        CollectionAssert.AreEqual(
            new[] { "https://a.invalid/feed.xml", "https://feeds.invalid/atom", "https://a.invalid/blog/feed.xml" },
            metadata.FeedUrls.ToArray());
    }

    [TestMethod]
    public async Task ParseHtmlAsync_ReturnsNullTitleWhenMissingOrBlank()
    {
        PageMetadata metadata = await PageParser.ParseHtmlAsync(Html("<html><head><title> </title></head></html>"));

        Assert.IsNull(metadata.Title);
        Assert.IsEmpty(metadata.FeedUrls);
    }

    [TestMethod]
    public async Task ParseHtmlAsync_DecodesLegacyEncodingFromContentType()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] content = Encoding.GetEncoding("shift_jis").GetBytes("<html><head><title>日本語のサイト</title></head></html>");
        WebPage page = new(new Uri("https://a.invalid/"), "text/html; charset=Shift_JIS", content);

        PageMetadata metadata = await PageParser.ParseHtmlAsync(page);

        Assert.AreEqual("日本語のサイト", metadata.Title);
    }

    [TestMethod]
    [DataRow("""<?xml version="1.0"?><rss version="2.0"><channel /></rss>""", true)]
    [DataRow("""<feed xmlns="http://www.w3.org/2005/Atom"></feed>""", true)]
    [DataRow("""<rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"></rdf:RDF>""", true)]
    [DataRow("""<!DOCTYPE rss [<!ENTITY x SYSTEM "file:///c:/windows/win.ini">]><rss>&x;</rss>""", true)]
    [DataRow("<html><body>Not found</body></html>", false)]
    [DataRow("not xml", false)]
    public void IsFeed_ChecksRootElement(string content, bool expected)
    {
        WebPage page = new(new Uri("https://a.invalid/feed"), "application/xml", Encoding.UTF8.GetBytes(content));

        Assert.AreEqual(expected, PageParser.IsFeed(page));
    }

    private static WebPage Html(string html, string finalUrl = "https://a.invalid/") =>
        new(new Uri(finalUrl), "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));
}
