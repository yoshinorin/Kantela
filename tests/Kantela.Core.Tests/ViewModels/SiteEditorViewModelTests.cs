using Kantela.Core.Models;
using Kantela.Core.ViewModels;

namespace Kantela.Core.Tests.ViewModels;

[TestClass]
public sealed class SiteEditorViewModelTests
{
    [TestMethod]
    [DataRow("Title", "https://a.example/", "", true)]
    [DataRow("Title", "https://a.example/", "https://a.example/feed", true)]
    [DataRow(" ", "https://a.example/", "", false)]
    [DataRow("Title", "a.example", "", false)]
    [DataRow("Title", "https://a.example/", "feed", false)]
    public void CanSave_ReflectsValidity(string title, string url, string feedUrl, bool expected)
    {
        SiteEditorViewModel editor = new() { Title = title, Url = url, FeedUrl = feedUrl };

        Assert.AreEqual(expected, editor.CanSave);
    }

    [TestMethod]
    public void EditingExistingSite_StartsWithItsValues()
    {
        SiteItemViewModel site = new(new Site { Id = 1, Title = "A", Url = "https://a.example/", FeedUrl = null });

        SiteEditorViewModel editor = new(site);

        Assert.IsFalse(editor.IsNew);
        Assert.AreEqual("A", editor.Title);
        Assert.AreEqual("https://a.example/", editor.Url);
        Assert.AreEqual(string.Empty, editor.FeedUrl);
    }
}
