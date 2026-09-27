using System;
using System.IO;
using System.Threading.Tasks;
using Kantela.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace Kantela;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1280, 800));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Kantela.ico"));
    }

    public MainViewModel ViewModel { get; }

    private async void Root_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private SiteItemViewModel? SelectedSite => SitesListView.SelectedItem as SiteItemViewModel;

    private async void Sites_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePreviewHeader();
        await ShowPreviewAsync();
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

    private async void Sites_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move)
        {
            await ViewModel.SaveOrderAsync();
        }
    }

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
