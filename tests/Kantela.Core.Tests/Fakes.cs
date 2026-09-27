using System.Text;
using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Kantela.Core.Services.Web;
using Kantela.Core.ViewModels;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeBrowserLauncher : IBrowserLauncher
{
    public bool Result { get; set; } = true;

    public List<Uri> OpenedUris { get; } = [];

    public Task<bool> OpenAsync(Uri uri)
    {
        OpenedUris.Add(uri);
        return Task.FromResult(Result);
    }
}

internal sealed class FakeFilePicker : IFilePicker
{
    public string? Path { get; set; }

    public Task<string?> PickOpenFileAsync(BookmarkFormat format) => Task.FromResult(Path);

    public Task<string?> PickSaveFileAsync(BookmarkFormat format, string suggestedFileName) => Task.FromResult(Path);
}

internal sealed class FakeDialogService : IDialogService
{
    // Each queued action edits the editor and returns whether the user pressed Save (false cancels).
    public Queue<Func<SiteEditorViewModel, bool>> EditorResponses { get; } = new();

    public ImportMode? SelectedImportMode { get; set; } = ImportMode.Merge;

    public bool ConfirmResult { get; set; } = true;

    public List<string> Messages { get; } = [];

    public List<string> Errors { get; } = [];

    // Error messages shown in the editor after failed saves.
    public List<string> EditorErrors { get; } = [];

    public async Task ShowSiteEditorAsync(SiteEditorViewModel editor)
    {
        while (EditorResponses.TryDequeue(out Func<SiteEditorViewModel, bool>? respond) && respond(editor))
        {
            if (await editor.SaveAsync())
            {
                return;
            }

            EditorErrors.Add(editor.ErrorMessage!);
        }
    }

    public Task<bool> ConfirmAsync(string title, string message, string primaryButtonText) =>
        Task.FromResult(ConfirmResult);

    public Task<ImportMode?> ChooseImportModeAsync() => Task.FromResult(SelectedImportMode);

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string message)
    {
        Errors.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class ListLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

internal sealed class TestDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kantela-tests", Guid.NewGuid().ToString("N"));

    public TestDirectory() => Directory.CreateDirectory(Path);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

internal sealed class FakeWebClient : IWebClient
{
    private readonly Dictionary<string, WebPage> _pages = new(StringComparer.Ordinal);

    public List<Uri> Requests { get; } = [];

    public void AddHtml(string url, string html, string? finalUrl = null) =>
        _pages[url] = new WebPage(new Uri(finalUrl ?? url), "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));

    public void AddFeed(string url) =>
        _pages[url] = new WebPage(new Uri(url), "application/rss+xml", Encoding.UTF8.GetBytes("""<rss version="2.0"><channel /></rss>"""));

    public Task<WebPage?> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        Requests.Add(uri);
        return Task.FromResult(_pages.GetValueOrDefault(uri.AbsoluteUri));
    }
}
