using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kantela.Core;
using Kantela.Core.Data;
using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Kantela.Core.Services.Web;
using Kantela.Core.ViewModels;
using Kantela.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using ZLogger;
using ZLogger.Providers;

namespace Kantela;

public partial class App : Application
{
    private readonly ILogger<App> _logger;
    private readonly BackupService _backupService;
    private Window? _window;
    private int _exited;

    public App()
    {
        InitializeComponent();

        Paths = AppPaths.CreateDefault();
        Paths.EnsureDirectories();
        LoggerFactory = CreateLoggerFactory(Paths);
        DbContextFactory = KantelaDbContextFactory.ForFile(Paths.Database);
        _logger = LoggerFactory.CreateLogger<App>();
        _backupService = new BackupService(
            DbContextFactory, Paths.Backups, TimeProvider.System, LoggerFactory.CreateLogger<BackupService>());

        UnhandledException += (_, e) => OnExit("unhandled exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnExit("unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => _logger.LogError(e.Exception, "Unobserved task exception");
    }

    public static new App Current => (App)Application.Current;

    public AppPaths Paths { get; }

    public ILoggerFactory LoggerFactory { get; }

    public IDbContextFactory<KantelaDbContext> DbContextFactory { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _logger.LogInformation("Starting. Data directory: {DataDirectory}", Paths.Root);

        using (KantelaDbContext db = DbContextFactory.CreateDbContext())
        {
            db.Database.Migrate();
        }

        string version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        HttpWebClient webClient = new(HttpWebClient.CreateHttpClient(version), LoggerFactory.CreateLogger<HttpWebClient>());

        MainWindow? window = null;
        MainViewModel viewModel = new(
            new SiteService(DbContextFactory, TimeProvider.System, LoggerFactory.CreateLogger<SiteService>()),
            new SiteInspector(webClient, LoggerFactory.CreateLogger<SiteInspector>()),
            new BookmarkTransferService(
                DbContextFactory, _backupService, TimeProvider.System, LoggerFactory.CreateLogger<BookmarkTransferService>()),
            new SettingsService(Paths.Settings, LoggerFactory.CreateLogger<SettingsService>()),
            new BrowserLauncher(),
            new FilePicker(() => window!.AppWindow.Id),
            new DialogService(() => window!.Content.XamlRoot),
            LoggerFactory.CreateLogger<MainViewModel>());
        window = new MainWindow(viewModel);
        window.Closed += (_, _) => OnExit("window closed", null);
        _window = window;
        _window.Activate();
    }

    // Runs at most once, from either a normal exit or a crash.
    private void OnExit(string reason, Exception? exception)
    {
        if (Interlocked.Exchange(ref _exited, 1) == 1)
        {
            return;
        }

        if (exception is not null)
        {
            _logger.LogCritical(exception, "Exiting due to {Reason}", reason);
        }
        else
        {
            _logger.LogInformation("Exiting due to {Reason}", reason);
        }

        try
        {
            _backupService.CreateBackup();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup on exit");
        }

        LoggerFactory.Dispose();
    }

    private static ILoggerFactory CreateLoggerFactory(AppPaths paths) =>
        Microsoft.Extensions.Logging.LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, sequenceNumber) =>
                    Path.Combine(paths.Logs, $"kantela-{timestamp.ToLocalTime():yyyyMMdd}_{sequenceNumber:000}.log");
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB = 1024;
                options.UsePlainTextFormatter(formatter =>
                    formatter.SetPrefixFormatter(
                        $"{0:local-longdate} [{1:short}] {2} ",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel, info.Category)));
            });
        });
}
