using CommunityToolkit.Mvvm.ComponentModel;
using Kantela.Core.Services;

namespace Kantela.Core.ViewModels;

// save checks and stores the input; it throws to reject it, and its exception message is shown in the editor.
public sealed partial class SiteEditorViewModel(Func<SiteInput, CancellationToken, Task> save) : ObservableObject
{
    private CancellationTokenSource? _saving;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _alias = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _feedUrl = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public SiteEditorViewModel(SiteItemViewModel site, Func<SiteInput, CancellationToken, Task> save)
        : this(save)
    {
        IsNew = false;
        _title = site.Title;
        _alias = site.Alias ?? string.Empty;
        _url = site.Url;
        _feedUrl = site.FeedUrl ?? string.Empty;
    }

    public bool IsNew { get; } = true;

    public string DialogTitle => IsNew ? "Add site" : "Edit site";

    public bool HasError => ErrorMessage is not null;

    public bool CanSave =>
        !IsBusy
        && UrlValidator.IsWebUrl(Url)
        && (string.IsNullOrWhiteSpace(FeedUrl) || UrlValidator.IsWebUrl(FeedUrl));

    public SiteInput ToInput() => new(Title, Url, FeedUrl, Alias);

    // Returns true when the site was saved; otherwise ErrorMessage tells why (unless it was cancelled).
    public async Task<bool> SaveAsync()
    {
        using CancellationTokenSource saving = new();
        _saving = saving;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await save(ToInput(), saving.Token);
            return true;
        }
        catch (OperationCanceledException) when (saving.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            _saving = null;
            IsBusy = false;
        }
    }

    public void CancelSave() => _saving?.Cancel();
}
