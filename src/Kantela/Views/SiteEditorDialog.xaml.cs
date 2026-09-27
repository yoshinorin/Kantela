using Kantela.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Kantela.Views;

public sealed partial class SiteEditorDialog : ContentDialog
{
    public SiteEditorDialog(SiteEditorViewModel editor)
    {
        Editor = editor;
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Closing += OnClosing;
    }

    public SiteEditorViewModel Editor { get; }

    // Keeps the dialog open while saving, and after a failure so that the input can be fixed.
    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        try
        {
            args.Cancel = !await Editor.SaveAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (args.Result != ContentDialogResult.Primary)
        {
            Editor.CancelSave();
        }
    }
}
