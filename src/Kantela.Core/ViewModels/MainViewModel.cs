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
    SiteInspector siteInspector,
    BookmarkTransferService transferService,
    SettingsService settingsService,
    IBrowserLauncher browserLauncher,
    IFilePicker filePicker,
    IDialogService dialogService,
    ILogger<MainViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    private SiteSort _sort = settingsService.Load().Sort;

    public ObservableCollection<SiteItemViewModel> Sites { get; } = [];

    public async Task LoadAsync()
    {
        IReadOnlyList<Site> sites = await siteService.GetAllAsync();
        Sites.Clear();
        foreach (SiteItemViewModel site in Sorted(sites.Select(s => new SiteItemViewModel(s)), Sort))
        {
            Sites.Add(site);
        }

        Renumber();
    }

    // Choosing the current column reverses the direction; another column starts from its default direction.
    // Items are moved rather than re-created so that the view can keep its selection.
    public void SortBy(SiteSortColumn column)
    {
        Sort = column == Sort.Column
            ? Sort with { Descending = !Sort.Descending }
            : new SiteSort(column, SiteSort.DefaultDescending(column));

        List<SiteItemViewModel> sorted = Sorted(Sites, Sort);
        for (int i = 0; i < sorted.Count; i++)
        {
            int current = Sites.IndexOf(sorted[i]);
            if (current != i)
            {
                Sites.Move(current, i);
            }
        }

        try
        {
            settingsService.Save(new AppSettings { Sort = Sort });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to save settings");
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

    // The editor stays open until the site is saved or the user cancels; errors are shown in the editor.
    [RelayCommand]
    private Task AddAsync() => RunAsync(() => dialogService.ShowSiteEditorAsync(new SiteEditorViewModel(
        (input, cancellationToken) => SaveSiteAsync(input, null, cancellationToken))));

    [RelayCommand]
    private Task EditAsync(SiteItemViewModel site) => RunAsync(() => dialogService.ShowSiteEditorAsync(new SiteEditorViewModel(
        site, (input, cancellationToken) => SaveSiteAsync(input, site, cancellationToken))));

    [RelayCommand]
    private Task DeleteAsync(SiteItemViewModel site) => RunAsync(async () =>
    {
        bool confirmed = await dialogService.ConfirmAsync("Delete site", $"Delete '{site.DisplayName}'?", "Delete");
        if (!confirmed)
        {
            return;
        }

        await siteService.DeleteAsync(site.Id);
        Sites.Remove(site);
        Renumber();
    });

    [RelayCommand]
    private Task ImportJsonAsync() => ImportAsync(BookmarkFormat.Json);

    [RelayCommand]
    private Task ImportOpmlAsync() => ImportAsync(BookmarkFormat.Opml);

    [RelayCommand]
    private Task ExportJsonAsync() => ExportAsync(BookmarkFormat.Json);

    [RelayCommand]
    private Task ExportOpmlAsync() => ExportAsync(BookmarkFormat.Opml);

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

    // The duplicate check runs first so that a registered site is not fetched again.
    private async Task SaveSiteAsync(SiteInput input, SiteItemViewModel? existing, CancellationToken cancellationToken)
    {
        try
        {
            await siteService.EnsureUrlIsAvailableAsync(input.Url, existing?.Id, cancellationToken);
            SiteInput? current = existing is null ? null : new SiteInput(existing.Title, existing.Url, existing.FeedUrl);
            SiteInput inspected = await siteInspector.InspectAsync(input, current, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (existing is null)
            {
                Site site = await siteService.AddAsync(inspected, CancellationToken.None);
                Sites.Add(new SiteItemViewModel(site) { Number = Sites.Count + 1 });
            }
            else
            {
                existing.Apply(await siteService.UpdateAsync(existing.Id, inspected, CancellationToken.None));
            }
        }
        catch (Exception ex) when (ex is not SiteRegistrationException and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to save site {Url}", input.Url);
            throw;
        }
    }

    private void Renumber()
    {
        int number = 1;
        foreach (SiteItemViewModel site in Sites.OrderBy(s => s.Id))
        {
            site.Number = number++;
        }
    }

    // Registration order (Id) breaks ties. Sites without the sorted date always come last.
    private static List<SiteItemViewModel> Sorted(IEnumerable<SiteItemViewModel> sites, SiteSort sort)
    {
        int direction = sort.Descending ? -1 : 1;
        List<SiteItemViewModel> sorted = [.. sites];
        sorted.Sort((a, b) =>
        {
            int result = sort.Column switch
            {
                SiteSortColumn.Title => direction * StringComparer.CurrentCultureIgnoreCase.Compare(a.DisplayName, b.DisplayName),
                SiteSortColumn.Previewed => CompareDates(a.LastPreviewedAt, b.LastPreviewedAt, direction),
                SiteSortColumn.Visited => CompareDates(a.LastVisitedAt, b.LastVisitedAt, direction),
                _ => 0,
            };
            return result != 0 ? result : (sort.Column == SiteSortColumn.Added ? direction : 1) * a.Id.CompareTo(b.Id);
        });
        return sorted;
    }

    private static int CompareDates(DateTime? a, DateTime? b, int direction) => (a, b) switch
    {
        (null, null) => 0,
        (null, _) => 1,
        (_, null) => -1,
        _ => direction * a.Value.CompareTo(b.Value),
    };

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
