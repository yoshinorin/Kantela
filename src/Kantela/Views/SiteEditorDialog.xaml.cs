using Kantela.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Kantela.Views;

public sealed partial class SiteEditorDialog : ContentDialog
{
    public SiteEditorDialog(SiteEditorViewModel editor)
    {
        Editor = editor;
        InitializeComponent();
    }

    public SiteEditorViewModel Editor { get; }
}
