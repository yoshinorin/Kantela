using Kantela.Core.Services;

namespace Kantela.Core.Tests.Services;

[TestClass]
public sealed class UrlNormalizerTests
{
    [TestMethod]
    [DataRow("https://a.invalid/", "http://a.invalid")]
    [DataRow("https://a.invalid/blog/", "https://A.INVALID/blog")]
    [DataRow("https://a.invalid:443/", "https://a.invalid/#top")]
    [DataRow(" https://a.invalid/?p=1 ", "http://a.invalid/?p=1")]
    public void ComparisonKey_IsEqualForSameSite(string a, string b)
    {
        Assert.AreEqual(UrlNormalizer.ComparisonKey(a), UrlNormalizer.ComparisonKey(b));
    }

    [TestMethod]
    [DataRow("https://a.invalid/", "https://www.a.invalid/")]
    [DataRow("https://a.invalid/Blog", "https://a.invalid/blog")]
    [DataRow("https://a.invalid/?p=1", "https://a.invalid/?p=2")]
    [DataRow("https://a.invalid/", "https://a.invalid:8443/")]
    public void ComparisonKey_DiffersForDifferentSites(string a, string b)
    {
        Assert.AreNotEqual(UrlNormalizer.ComparisonKey(a), UrlNormalizer.ComparisonKey(b));
    }
}
