using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Kantela.Core.Models;

namespace Kantela.Core.ViewModels;

public sealed partial class SiteItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _alias;

    [ObservableProperty]
    private string _url;

    [ObservableProperty]
    private string? _feedUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastVisitedText))]
    private DateTime? _lastVisitedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastPreviewedText))]
    private DateTime? _lastPreviewedAt;

    [ObservableProperty]
    private FaviconImage? _icon;

    // 1-based position in registration (Id) order among the listed sites. Maintained by MainViewModel.
    [ObservableProperty]
    private int _number;

    public SiteItemViewModel(Site site)
    {
        Id = site.Id;
        _title = site.Title;
        _alias = site.Alias;
        _url = site.Url;
        _feedUrl = site.FeedUrl;
        _lastVisitedAt = site.LastVisitedAt;
        _lastPreviewedAt = site.LastPreviewedAt;
    }

    public int Id { get; }

    public string DisplayName => Alias ?? Title;

    public string LastVisitedText => FormatDate(LastVisitedAt);

    public string LastPreviewedText => FormatDate(LastPreviewedAt);

    public void Apply(Site site)
    {
        Title = site.Title;
        Alias = site.Alias;
        Url = site.Url;
        FeedUrl = site.FeedUrl;
        LastVisitedAt = site.LastVisitedAt;
        LastPreviewedAt = site.LastPreviewedAt;
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";
}
