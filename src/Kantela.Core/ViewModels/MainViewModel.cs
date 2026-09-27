using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kantela.Core.Models;
using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.ViewModels;

public sealed partial class MainViewModel(
    SiteService siteService,
    BookmarkTransferService transferService,
    IBrowserLauncher browserLauncher,
    IFilePicker filePicker,
    IDialogService dialogService,
    ILogger<MainViewModel> logger) : ObservableObject
{
    public ObservableCollection<SiteItemViewModel> Sites { get; } = [];

    public async Task LoadAsync()
    {
        IReadOnlyList<Site> sites = await siteService.GetAllAsync();
        Sites.Clear();
        foreach (Site site in sites)
        {
            Sites.Add(new SiteItemViewModel(site));
        }
    }

    [RelayCommand]
    private Task OpenAsync(SiteItemViewModel site) => RunAsync(async () =>
    {
        if (!await browserLauncher.OpenAsync(new Uri(site.Url)))
        {
            await dialogService.ShowErrorAsync($"Could not open '{site.Url}'.");
            return;
        }

        site.LastVisitedAt = await siteService.MarkVisitedAsync(site.Id);
    });

    [RelayCommand]
    private Task RecordPreviewAsync(SiteItemViewModel site) => RunAsync(async () =>
        site.LastPreviewedAt = await siteService.MarkPreviewedAsync(site.Id));

    [RelayCommand]
    private Task AddAsync() => RunAsync(async () =>
    {
        SiteEditorViewModel editor = new();
        while (await dialogService.ShowSiteEditorAsync(editor))
        {
            try
            {
                Site site = await siteService.AddAsync(editor.ToInput());
                Sites.Add(new SiteItemViewModel(site));
                return;
            }
            catch (DuplicateSiteUrlException ex)
            {
                await dialogService.ShowErrorAsync($"'{ex.Url}' is already registered.");
            }
        }
    });

    [RelayCommand]
    private Task EditAsync(SiteItemViewModel site) => RunAsync(async () =>
    {
        SiteEditorViewModel editor = new(site);
        while (await dialogService.ShowSiteEditorAsync(editor))
        {
            try
            {
                site.Apply(await siteService.UpdateAsync(site.Id, editor.ToInput()));
                return;
            }
            catch (DuplicateSiteUrlException ex)
            {
                await dialogService.ShowErrorAsync($"'{ex.Url}' is already registered.");
            }
        }
    });

    [RelayCommand]
    private Task DeleteAsync(SiteItemViewModel site) => RunAsync(async () =>
    {
        bool confirmed = await dialogService.ConfirmAsync("Delete site", $"Delete '{site.Title}'?", "Delete");
        if (!confirmed)
        {
            return;
        }

        await siteService.DeleteAsync(site.Id);
        Sites.Remove(site);
    });

    [RelayCommand]
    private Task ImportJsonAsync() => ImportAsync(BookmarkFormat.Json);

    [RelayCommand]
    private Task ImportOpmlAsync() => ImportAsync(BookmarkFormat.Opml);

    [RelayCommand]
    private Task ExportJsonAsync() => ExportAsync(BookmarkFormat.Json);

    [RelayCommand]
    private Task ExportOpmlAsync() => ExportAsync(BookmarkFormat.Opml);

    public Task SaveOrderAsync() => RunAsync(() => siteService.ReorderAsync(Sites.Select(s => s.Id).ToList()));

    private Task ImportAsync(BookmarkFormat format) => RunAsync(async () =>
    {
        string? path = await filePicker.PickOpenFileAsync(format);
        if (path is null)
        {
            return;
        }

        ImportMode? mode = await dialogService.ChooseImportModeAsync();
        if (mode is null)
        {
            return;
        }

        ImportResult result = await transferService.ImportAsync(format, path, mode.Value);
        await LoadAsync();
        await dialogService.ShowMessageAsync(
            "Import completed",
            $"Added: {result.Added}\n"
            + $"Skipped (already registered): {result.SkippedDuplicates}\n"
            + $"Skipped (invalid URL): {result.SkippedInvalid}");
    });

    private Task ExportAsync(BookmarkFormat format) => RunAsync(async () =>
    {
        string? path = await filePicker.PickSaveFileAsync(format, "kantela");
        if (path is null)
        {
            return;
        }

        ExportResult result = await transferService.ExportAsync(format, path);
        string message = $"Exported: {result.Exported}";
        if (result.Skipped > 0)
        {
            message += $"\nNot exported (no feed URL): {result.Skipped}";
        }

        await dialogService.ShowMessageAsync("Export completed", message);
    });

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Operation failed");
            await dialogService.ShowErrorAsync(ex.Message);
        }
    }
}
