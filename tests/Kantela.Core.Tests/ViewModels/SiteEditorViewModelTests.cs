using Kantela.Core.Models;
using Kantela.Core.ViewModels;

namespace Kantela.Core.Tests.ViewModels;

[TestClass]
public sealed class SiteEditorViewModelTests
{
    [TestMethod]
    [DataRow("Title", "https://a.invalid/", "", true)]
    [DataRow("Title", "https://a.invalid/", "https://a.invalid/feed", true)]
    [DataRow(" ", "https://a.invalid/", "", false)]
    [DataRow("Title", "a.invalid", "", false)]
    [DataRow("Title", "https://a.invalid/", "feed", false)]
    public void CanSave_ReflectsValidity(string title, string url, string feedUrl, bool expected)
    {
        SiteEditorViewModel editor = new() { Title = title, Url = url, FeedUrl = feedUrl };

        Assert.AreEqual(expected, editor.CanSave);
    }

    [TestMethod]
    public void EditingExistingSite_StartsWithItsValues()
    {
        SiteItemViewModel site = new(new Site { Id = 1, Title = "A", Url = "https://a.invalid/", FeedUrl = null });

        SiteEditorViewModel editor = new(site);

        Assert.IsFalse(editor.IsNew);
        Assert.AreEqual("A", editor.Title);
        Assert.AreEqual("https://a.invalid/", editor.Url);
        Assert.AreEqual(string.Empty, editor.FeedUrl);
    }
}
