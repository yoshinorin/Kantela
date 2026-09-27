using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;

namespace Kantela.Views;

// Read-only preview of a site. The page cannot be clicked or typed into; only wheel scrolling
// is forwarded, and a click on the page raises Clicked. Browsing data is not kept: the WebView2 runs InPrivate and its user data
// folder is deleted before first use.
public sealed partial class SitePreview : UserControl
{
    private readonly ILogger<SitePreview> _logger = App.Current.LoggerFactory.CreateLogger<SitePreview>();
    private Task<bool>? _initialization;
    private bool _awaitingNavigation;
    private ulong? _allowedNavigationId;
    private bool _isShowingSite;

    public SitePreview()
    {
        InitializeComponent();
    }

    // Raised when the page of the site passed to ShowAsync has loaded successfully.
    public event EventHandler? PageLoaded;

    public event EventHandler? Clicked;

    public async Task ShowAsync(Uri? uri)
    {
        if (!await (_initialization ??= InitializeAsync()))
        {
            return;
        }

        CoreWebView2 core = PreviewView.CoreWebView2;
        _isShowingSite = uri is not null;
        ProtectedCursor = _isShowingSite ? InputSystemCursor.Create(InputSystemCursorShape.Hand) : null;
        if (uri is null)
        {
            Navigate(core, "about:blank");
            PreviewView.Visibility = Visibility.Collapsed;
            LoadingBar.Visibility = Visibility.Collapsed;
            ShowStatus("Select a site to preview.");
            return;
        }

        StatusText.Visibility = Visibility.Collapsed;
        PreviewView.Visibility = Visibility.Visible;
        LoadingBar.Visibility = Visibility.Visible;
        Navigate(core, uri.AbsoluteUri);
    }

    private async Task<bool> InitializeAsync()
    {
        try
        {
            DeleteUserData();
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                null, App.Current.Paths.WebView, new CoreWebView2EnvironmentOptions());
            CoreWebView2ControllerOptions controllerOptions = environment.CreateCoreWebView2ControllerOptions();
            controllerOptions.IsInPrivateModeEnabled = true;
            await PreviewView.EnsureCoreWebView2Async(environment, controllerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2");
            ShowStatus("Preview is unavailable. The WebView2 Runtime may not be installed.");
            return false;
        }

        CoreWebView2 core = PreviewView.CoreWebView2;
        core.IsMuted = true;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.NavigationStarting += Core_NavigationStarting;
        core.NavigationCompleted += (_, e) =>
        {
            if (e.NavigationId != _allowedNavigationId)
            {
                return;
            }

            LoadingBar.Visibility = Visibility.Collapsed;
            if (_isShowingSite && e.IsSuccess)
            {
                PageLoaded?.Invoke(this, EventArgs.Empty);
            }
        };
        return true;
    }

    private void DeleteUserData()
    {
        string path = App.Current.Paths.WebView;
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            // e.g. another Kantela instance is still using the folder
            _logger.LogWarning(ex, "Failed to delete WebView2 user data at {Path}", path);
        }
    }

    private void Navigate(CoreWebView2 core, string uri)
    {
        _awaitingNavigation = true;
        _allowedNavigationId = null;
        core.Navigate(uri);
    }

    // Allows only the navigation started by Navigate (redirects share its id); page-initiated
    // navigations are cancelled.
    private void Core_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_awaitingNavigation)
        {
            _awaitingNavigation = false;
            _allowedNavigationId = e.NavigationId;
            return;
        }

        if (e.NavigationId != _allowedNavigationId)
        {
            e.Cancel = true;
        }
    }

    private async void InputBlocker_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        if (PreviewView.Visibility != Visibility.Visible || PreviewView.CoreWebView2 is not CoreWebView2 core)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(PreviewView);
        int delta = -point.Properties.MouseWheelDelta;
        bool horizontal = point.Properties.IsHorizontalMouseWheel;
        string parameters = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"type\":\"mouseWheel\",\"x\":{0},\"y\":{1},\"deltaX\":{2},\"deltaY\":{3}}}",
            point.Position.X,
            point.Position.Y,
            horizontal ? -delta : 0,
            horizontal ? 0 : delta);
        try
        {
            await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", parameters);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to forward a wheel event to the preview");
        }
    }

    private void InputBlocker_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_isShowingSite)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }
}
