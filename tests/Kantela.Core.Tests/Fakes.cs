using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
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
    // Each queued action edits the editor and returns whether the user saved.
    public Queue<Func<SiteEditorViewModel, bool>> EditorResponses { get; } = new();

    public ImportMode? SelectedImportMode { get; set; } = ImportMode.Merge;

    public bool ConfirmResult { get; set; } = true;

    public List<string> Messages { get; } = [];

    public List<string> Errors { get; } = [];

    public Task<bool> ShowSiteEditorAsync(SiteEditorViewModel editor) =>
        Task.FromResult(EditorResponses.TryDequeue(out Func<SiteEditorViewModel, bool>? respond) && respond(editor));

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
