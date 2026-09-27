using System;
using System.IO;
using Kantela.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        AppWindow.Resize(new SizeInt32(640, 800));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Kantela.ico"));
    }

    public MainViewModel ViewModel { get; }

    private async void Root_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private void Sites_ItemClick(object sender, ItemClickEventArgs e) =>
        ViewModel.OpenCommand.Execute((SiteItemViewModel)e.ClickedItem);

    private async void Sites_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move)
        {
            await ViewModel.SaveOrderAsync();
        }
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenCommand.Execute(GetSite(sender));

    private void EditMenuItem_Click(object sender, RoutedEventArgs e) =>
        ViewModel.EditCommand.Execute(GetSite(sender));

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteCommand.Execute(GetSite(sender));

    private async void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchFolderPathAsync(App.Current.Paths.Root);

    private static SiteItemViewModel GetSite(object sender) =>
        (SiteItemViewModel)((FrameworkElement)sender).DataContext;
}
