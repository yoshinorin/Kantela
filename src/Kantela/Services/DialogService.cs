using System;
using System.Threading.Tasks;
using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Kantela.Core.ViewModels;
using Kantela.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kantela.Services;

internal sealed class DialogService(Func<XamlRoot> xamlRootProvider) : IDialogService
{
    public async Task ShowSiteEditorAsync(SiteEditorViewModel editor)
    {
        SiteEditorDialog dialog = new(editor) { XamlRoot = xamlRootProvider() };
        await dialog.ShowAsync();
    }

    public async Task<ImportMode?> ChooseImportModeAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = xamlRootProvider(),
            Title = "Import",
            Content = "Merge: add sites that are not registered yet.\n"
                + "Replace: delete all current sites and import. A backup is created first.",
            PrimaryButtonText = "Merge",
            SecondaryButtonText = "Replace",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => ImportMode.Merge,
            ContentDialogResult.Secondary => ImportMode.Replace,
            _ => null,
        };
    }

    public async Task<bool> ConfirmAsync(string title, string message, string primaryButtonText)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = xamlRootProvider(),
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public Task ShowMessageAsync(string title, string message) => ShowAsync(title, message);

    public Task ShowErrorAsync(string message) => ShowAsync("Error", message);

    private async Task ShowAsync(string title, string message)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = xamlRootProvider(),
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
    }
}
