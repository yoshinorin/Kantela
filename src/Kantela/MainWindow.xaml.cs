using System;
using System.IO;
using System.Threading.Tasks;
using Kantela.Core.Models;
using Kantela.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace Kantela;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        UpdateSortHeaders();
        AppWindow.Resize(new SizeInt32(1280, 800));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Kantela.ico"));
    }

    private SiteItemViewModel? _previewedSite;
    private bool _isSorting;

    public MainViewModel ViewModel { get; }

    private async void Root_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private SiteItemViewModel? SelectedSite => SitesListView.SelectedItem as SiteItemViewModel;

    // Reads the current selection instead of the event args, so that the transient changes caused by
    // moving items while sorting are ignored even if they are reported after sorting has finished.
    private async void Sites_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSorting || SelectedSite == _previewedSite)
        {
            return;
        }

        _previewedSite = SelectedSite;
        UpdatePreviewHeader();
        await ShowPreviewAsync();
    }

    private void SortHeader_Click(object sender, RoutedEventArgs e)
    {
        SiteSortColumn column = Enum.Parse<SiteSortColumn>((string)((FrameworkElement)sender).Tag);
        SiteItemViewModel? selected = SelectedSite;
        _isSorting = true;
        try
        {
            ViewModel.SortBy(column);
            SitesListView.SelectedItem = selected;
        }
        finally
        {
            _isSorting = false;
        }

        UpdateSortHeaders();
        if (selected is not null)
        {
            SitesListView.ScrollIntoView(selected);
        }
    }

    private void UpdateSortHeaders()
    {
        SiteSort sort = ViewModel.Sort;
        UpdateSortIcon(TitleSortIcon, SiteSortColumn.Title, sort);
        UpdateSortIcon(AddedSortIcon, SiteSortColumn.Added, sort);
        UpdateSortIcon(PreviewedSortIcon, SiteSortColumn.Previewed, sort);
        UpdateSortIcon(VisitedSortIcon, SiteSortColumn.Visited, sort);
    }

    // Glyphs of Segoe Fluent Icons: E74A = Up, E74B = Down.
    private static void UpdateSortIcon(FontIcon icon, SiteSortColumn column, SiteSort sort)
    {
        icon.Visibility = sort.Column == column ? Visibility.Visible : Visibility.Collapsed;
        icon.Glyph = sort.Descending ? "\ue74b" : "\ue74a";
    }

    private void Sites_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is SiteItemViewModel site)
        {
            ViewModel.OpenCommand.Execute(site);
        }
    }

    private void Sites_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            OpenSelectedSite();
            e.Handled = true;
        }
    }

    private void OpenSelected_Click(object sender, RoutedEventArgs e) => OpenSelectedSite();

    private void Preview_Clicked(object? sender, EventArgs e) => OpenSelectedSite();

    private void Preview_PageLoaded(object? sender, EventArgs e)
    {
        if (SelectedSite is SiteItemViewModel site)
        {
            ViewModel.RecordPreviewCommand.Execute(site);
        }
    }

    private void OpenSelectedSite()
    {
        if (SelectedSite is SiteItemViewModel site)
        {
            ViewModel.OpenCommand.Execute(site);
        }
    }

    private void UpdatePreviewHeader()
    {
        PreviewHeader.Visibility = SelectedSite is null ? Visibility.Collapsed : Visibility.Visible;
        PreviewTitle.Text = SelectedSite?.Title ?? string.Empty;
    }

    private Task ShowPreviewAsync() =>
        Preview.ShowAsync(SelectedSite is SiteItemViewModel site && Uri.TryCreate(site.Url, UriKind.Absolute, out Uri? uri) ? uri : null);

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenCommand.Execute(GetSite(sender));

    private async void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SiteItemViewModel site = GetSite(sender);
        string url = site.Url;
        await ViewModel.EditCommand.ExecuteAsync(site);
        if (site != SelectedSite)
        {
            return;
        }

        UpdatePreviewHeader();
        if (site.Url != url)
        {
            await ShowPreviewAsync();
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteCommand.Execute(GetSite(sender));

    private async void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchFolderPathAsync(App.Current.Paths.Root);

    private static SiteItemViewModel GetSite(object sender) =>
        (SiteItemViewModel)((FrameworkElement)sender).DataContext;
}
