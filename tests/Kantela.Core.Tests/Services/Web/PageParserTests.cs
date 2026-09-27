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
    public async Task ParseHtmlAsync_ReturnsNullTitleWhenMissingOrBlankAndFallsBackToFaviconIco()
    {
        PageMetadata metadata = await PageParser.ParseHtmlAsync(Html("<html><head><title> </title></head></html>", "https://a.invalid/blog/post"));

        Assert.IsNull(metadata.Title);
        Assert.IsEmpty(metadata.FeedUrls);
        CollectionAssert.AreEqual(new[] { "https://a.invalid/favicon.ico" }, metadata.IconUrls.ToArray());
    }

    [TestMethod]
    public async Task ParseHtmlAsync_RanksIconsAndLimitsDeclaredOnes()
    {
        WebPage page = Html("""
            <link rel="apple-touch-icon" sizes="180x180" href="/apple.png">
            <link rel="icon" sizes="16x16" href="/16.png">
            <link rel="shortcut icon" href="/favicon.ico">
            <link rel="icon" sizes="32x32" href="/32.png">
            <link rel="icon" type="image/svg+xml" href="/icon.svg">
            <link rel="mask-icon" href="/mask.svg">
            <link rel="icon" href="data:image/png;base64,AAAA">
            """);

        PageMetadata metadata = await PageParser.ParseHtmlAsync(page);

        CollectionAssert.AreEqual(
            new[] { "https://a.invalid/icon.svg", "https://a.invalid/32.png", "https://a.invalid/favicon.ico" },
            metadata.IconUrls.ToArray());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }, "image/png")]
    [DataRow(new byte[] { 0, 0, 1, 0, 1, 0 }, "image/x-icon")]
    [DataRow(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
    [DataRow(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [DataRow(new byte[] { 0x42, 0x4D, 0, 0 }, "image/bmp")]
    [DataRow(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [DataRow(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E }, null)]
    [DataRow(new byte[] { }, null)]
    public void ImageContentType_DetectsRasterFormats(byte[] data, string? expected)
    {
        Assert.AreEqual(expected, PageParser.ImageContentType(data));
    }

    [TestMethod]
    [DataRow("""<?xml version="1.0"?><svg xmlns="http://www.w3.org/2000/svg"></svg>""", "image/svg+xml")]
    [DataRow("""<!DOCTYPE html><html><body><svg></svg></body></html>""", null)]
    public void ImageContentType_DetectsSvgButNotHtml(string content, string? expected)
    {
        Assert.AreEqual(expected, PageParser.ImageContentType(Encoding.UTF8.GetBytes(content)));
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
