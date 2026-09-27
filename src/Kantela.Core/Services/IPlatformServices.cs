using Kantela.Core.Services.Transfer;
using Kantela.Core.ViewModels;

namespace Kantela.Core.Services;

public interface IBrowserLauncher
{
    Task<bool> OpenAsync(Uri uri);
}

public interface IFilePicker
{
    Task<string?> PickOpenFileAsync(BookmarkFormat format);

    Task<string?> PickSaveFileAsync(BookmarkFormat format, string suggestedFileName);
}

public interface IDialogService
{
    Task<bool> ShowSiteEditorAsync(SiteEditorViewModel editor);

    Task<ImportMode?> ChooseImportModeAsync();

    Task<bool> ConfirmAsync(string title, string message, string primaryButtonText);

    Task ShowMessageAsync(string title, string message);

    Task ShowErrorAsync(string message);
}
