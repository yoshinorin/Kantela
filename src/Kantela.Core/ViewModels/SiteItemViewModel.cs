using CommunityToolkit.Mvvm.ComponentModel;
using Kantela.Core.Models;

namespace Kantela.Core.ViewModels;

public sealed partial class SiteItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

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

    public SiteItemViewModel(Site site)
    {
        Id = site.Id;
        _title = site.Title;
        _url = site.Url;
        _feedUrl = site.FeedUrl;
        _lastVisitedAt = site.LastVisitedAt;
        _lastPreviewedAt = site.LastPreviewedAt;
    }

    public int Id { get; }

    public string LastVisitedText => LastVisitedAt is DateTime visitedAt
        ? $"Visited {visitedAt.ToLocalTime():yyyy-MM-dd HH:mm}"
        : "Not visited";

    public string LastPreviewedText => LastPreviewedAt is DateTime previewedAt
        ? $"Previewed {previewedAt.ToLocalTime():yyyy-MM-dd HH:mm}"
        : "Not previewed";

    public void Apply(Site site)
    {
        Title = site.Title;
        Url = site.Url;
        FeedUrl = site.FeedUrl;
        LastVisitedAt = site.LastVisitedAt;
        LastPreviewedAt = site.LastPreviewedAt;
    }
}
