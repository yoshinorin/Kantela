using CommunityToolkit.Mvvm.ComponentModel;
using Kantela.Core.Services;

namespace Kantela.Core.ViewModels;

public sealed partial class SiteEditorViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _feedUrl = string.Empty;

    public SiteEditorViewModel()
    {
        IsNew = true;
    }

    public SiteEditorViewModel(SiteItemViewModel site)
    {
        IsNew = false;
        _title = site.Title;
        _url = site.Url;
        _feedUrl = site.FeedUrl ?? string.Empty;
    }

    public bool IsNew { get; }

    public string DialogTitle => IsNew ? "Add site" : "Edit site";

    public bool CanSave =>
        !string.IsNullOrWhiteSpace(Title)
        && UrlValidator.IsWebUrl(Url)
        && (string.IsNullOrWhiteSpace(FeedUrl) || UrlValidator.IsWebUrl(FeedUrl));

    public SiteInput ToInput() => new(Title, Url, FeedUrl);
}
