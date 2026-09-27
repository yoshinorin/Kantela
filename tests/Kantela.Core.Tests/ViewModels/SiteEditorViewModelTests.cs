using Kantela.Core.Models;
using Kantela.Core.Services;
using Kantela.Core.ViewModels;

namespace Kantela.Core.Tests.ViewModels;

[TestClass]
public sealed class SiteEditorViewModelTests
{
    private static readonly Func<SiteInput, CancellationToken, Task> s_saved = (_, _) => Task.CompletedTask;

    [TestMethod]
    [DataRow("", "https://a.invalid/", "", true)]
    [DataRow("Title", "https://a.invalid/", "https://a.invalid/feed", true)]
    [DataRow("Title", "a.invalid", "", false)]
    [DataRow("Title", "https://a.invalid/", "feed", false)]
    public void CanSave_ReflectsValidity(string title, string url, string feedUrl, bool expected)
    {
        SiteEditorViewModel editor = new(s_saved) { Title = title, Url = url, FeedUrl = feedUrl };

        Assert.AreEqual(expected, editor.CanSave);
    }

    [TestMethod]
    public void EditingExistingSite_StartsWithItsValues()
    {
        SiteItemViewModel site = new(new Site { Id = 1, Title = "A", Alias = "Alias", Url = "https://a.invalid/", FeedUrl = null });

        SiteEditorViewModel editor = new(site, s_saved);

        Assert.IsFalse(editor.IsNew);
        Assert.AreEqual("A", editor.Title);
        Assert.AreEqual("Alias", editor.Alias);
        Assert.AreEqual("https://a.invalid/", editor.Url);
        Assert.AreEqual(string.Empty, editor.FeedUrl);
    }

    [TestMethod]
    public async Task SaveAsync_ShowsErrorMessageAndClearsItOnNextAttempt()
    {
        bool fail = true;
        SiteEditorViewModel editor = new((_, _) => fail ? throw new SiteRegistrationException("Rejected") : Task.CompletedTask)
        {
            Url = "https://a.invalid/",
        };

        Assert.IsFalse(await editor.SaveAsync());
        Assert.AreEqual("Rejected", editor.ErrorMessage);
        Assert.IsTrue(editor.HasError);

        fail = false;
        Assert.IsTrue(await editor.SaveAsync());
        Assert.IsNull(editor.ErrorMessage);
        Assert.IsFalse(editor.IsBusy);
    }

    [TestMethod]
    public async Task CancelSave_StopsSavingWithoutError()
    {
        TaskCompletionSource started = new();
        SiteEditorViewModel editor = new(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        });

        Task<bool> saving = editor.SaveAsync();
        await started.Task;
        Assert.IsTrue(editor.IsBusy);
        Assert.IsFalse(editor.CanSave);
        editor.CancelSave();

        Assert.IsFalse(await saving);
        Assert.IsNull(editor.ErrorMessage);
    }
}
