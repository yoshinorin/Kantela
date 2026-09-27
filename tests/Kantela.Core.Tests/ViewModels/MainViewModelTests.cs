using Kantela.Core.Models;
using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Kantela.Core.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kantela.Core.Tests.ViewModels;

[TestClass]
public sealed class MainViewModelTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    private TestDatabase _database = null!;
    private TestDirectory _directory = null!;
    private SiteService _service = null!;
    private FakeBrowserLauncher _browser = null!;
    private FakeFilePicker _filePicker = null!;
    private FakeDialogService _dialogs = null!;
    private BookmarkTransferService _transferService = null!;
    private MainViewModel _viewModel = null!;

    [TestInitialize]
    public void Initialize()
    {
        _database = new TestDatabase();
        _directory = new TestDirectory();
        _service = new SiteService(_database.Factory, new FixedTimeProvider(s_now), NullLogger<SiteService>.Instance);
        _browser = new FakeBrowserLauncher();
        _filePicker = new FakeFilePicker();
        _dialogs = new FakeDialogService();
        _transferService = new(
            _database.Factory,
            new BackupService(_database.Factory, _directory.Path, TimeProvider.System, NullLogger<BackupService>.Instance),
            TimeProvider.System,
            NullLogger<BookmarkTransferService>.Instance);
        _viewModel = CreateViewModel();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _database.Dispose();
        _directory.Dispose();
    }

    [TestMethod]
    public async Task LoadAsync_LoadsSitesInOrder()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));
        await _service.AddAsync(new SiteInput("B", "https://b.invalid/", null));

        await _viewModel.LoadAsync();

        CollectionAssert.AreEqual(new[] { "A", "B" }, _viewModel.Sites.Select(s => s.Title).ToArray());
    }

    [TestMethod]
    public async Task Open_LaunchesBrowserAndRecordsVisit()
    {
        SiteItemViewModel site = await AddAndLoadAsync();

        await _viewModel.OpenCommand.ExecuteAsync(site);

        Assert.AreEqual(new Uri("https://a.invalid/"), _browser.OpenedUris.Single());
        Assert.AreEqual(s_now.UtcDateTime, site.LastVisitedAt);
        Assert.AreEqual(s_now.UtcDateTime, (await _service.GetAllAsync()).Single().LastVisitedAt);
    }

    [TestMethod]
    public async Task Open_DoesNotRecordVisitWhenLaunchFails()
    {
        SiteItemViewModel site = await AddAndLoadAsync();
        _browser.Result = false;

        await _viewModel.OpenCommand.ExecuteAsync(site);

        Assert.IsNull(site.LastVisitedAt);
        Assert.HasCount(1, _dialogs.Errors);
    }

    [TestMethod]
    public async Task RecordPreview_RecordsPreviewWithoutLaunchingBrowser()
    {
        SiteItemViewModel site = await AddAndLoadAsync();

        await _viewModel.RecordPreviewCommand.ExecuteAsync(site);

        Assert.IsEmpty(_browser.OpenedUris);
        Assert.AreEqual(s_now.UtcDateTime, site.LastPreviewedAt);
        Assert.IsNull(site.LastVisitedAt);
        Assert.AreEqual(s_now.UtcDateTime, (await _service.GetAllAsync()).Single().LastPreviewedAt);
    }

    [TestMethod]
    public async Task Add_ReopensEditorOnDuplicateUrlUntilSaved()
    {
        await AddAndLoadAsync();
        _dialogs.EditorResponses.Enqueue(editor => Fill(editor, "Dup", "https://a.invalid/"));
        _dialogs.EditorResponses.Enqueue(editor => Fill(editor, "New", "https://b.invalid/"));

        await _viewModel.AddCommand.ExecuteAsync(null);

        Assert.HasCount(1, _dialogs.Errors);
        CollectionAssert.AreEqual(new[] { "A", "New" }, _viewModel.Sites.Select(s => s.Title).ToArray());
    }

    [TestMethod]
    public async Task Add_DoesNothingWhenCancelled()
    {
        await _viewModel.AddCommand.ExecuteAsync(null);

        Assert.IsEmpty(_viewModel.Sites);
        Assert.IsEmpty(await _service.GetAllAsync());
    }

    [TestMethod]
    public async Task Edit_UpdatesItem()
    {
        SiteItemViewModel site = await AddAndLoadAsync();
        _dialogs.EditorResponses.Enqueue(editor => Fill(editor, "Renamed", "https://a.invalid/"));

        await _viewModel.EditCommand.ExecuteAsync(site);

        Assert.AreEqual("Renamed", site.Title);
        Assert.AreEqual("Renamed", (await _service.GetAllAsync()).Single().Title);
    }

    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(false, 1)]
    public async Task Delete_RespectsConfirmation(bool confirmed, int expectedCount)
    {
        SiteItemViewModel site = await AddAndLoadAsync();
        _dialogs.ConfirmResult = confirmed;

        await _viewModel.DeleteCommand.ExecuteAsync(site);

        Assert.HasCount(expectedCount, _viewModel.Sites);
        Assert.HasCount(expectedCount, await _service.GetAllAsync());
    }

    [TestMethod]
    public async Task LoadAsync_SortsByRegistrationOrderByDefault()
    {
        await AddSitesAsync();

        await _viewModel.LoadAsync();

        Assert.AreEqual(new SiteSort(SiteSortColumn.Added, Descending: false), _viewModel.Sort);
        CollectionAssert.AreEqual(new[] { "b", "C", "a" }, Titles());
    }

    [TestMethod]
    [DataRow(SiteSortColumn.Title, false, new[] { "a", "b", "C" })]
    [DataRow(SiteSortColumn.Previewed, true, new[] { "C", "b", "a" })]
    [DataRow(SiteSortColumn.Visited, true, new[] { "a", "b", "C" })]
    public async Task SortBy_NewColumnStartsFromItsDefaultDirection(
        SiteSortColumn column, bool expectedDescending, string[] expected)
    {
        await AddSitesAsync();
        await _viewModel.LoadAsync();

        _viewModel.SortBy(column);

        Assert.AreEqual(new SiteSort(column, expectedDescending), _viewModel.Sort);
        CollectionAssert.AreEqual(expected, Titles());
    }

    [TestMethod]
    [DataRow(SiteSortColumn.Added, new[] { "a", "C", "b" })]
    [DataRow(SiteSortColumn.Visited, new[] { "b", "a", "C" })]
    public async Task SortBy_SameColumnReversesDirectionAndKeepsMissingDatesLast(SiteSortColumn column, string[] expected)
    {
        await AddSitesAsync();
        await _viewModel.LoadAsync();
        if (_viewModel.Sort.Column != column)
        {
            _viewModel.SortBy(column);
        }

        bool descending = _viewModel.Sort.Descending;
        _viewModel.SortBy(column);

        Assert.AreEqual(new SiteSort(column, !descending), _viewModel.Sort);
        CollectionAssert.AreEqual(expected, Titles());
    }

    [TestMethod]
    public async Task Numbers_FollowRegistrationOrderAcrossSortingAddingAndDeleting()
    {
        await AddSitesAsync();
        await _viewModel.LoadAsync();

        _viewModel.SortBy(SiteSortColumn.Title);
        CollectionAssert.AreEqual(new[] { 3, 1, 2 }, Numbers());

        await _viewModel.DeleteCommand.ExecuteAsync(_viewModel.Sites.Single(s => s.Title == "b"));
        CollectionAssert.AreEqual(new[] { 2, 1 }, Numbers());

        _dialogs.EditorResponses.Enqueue(editor => Fill(editor, "d", "https://d.invalid/"));
        await _viewModel.AddCommand.ExecuteAsync(null);
        CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Numbers());
    }

    [TestMethod]
    public async Task SortBy_IsRestoredByNextInstance()
    {
        await AddSitesAsync();
        _viewModel.SortBy(SiteSortColumn.Title);
        _viewModel.SortBy(SiteSortColumn.Title);

        _viewModel = CreateViewModel();
        await _viewModel.LoadAsync();

        Assert.AreEqual(new SiteSort(SiteSortColumn.Title, Descending: true), _viewModel.Sort);
        CollectionAssert.AreEqual(new[] { "C", "b", "a" }, Titles());
    }

    [TestMethod]
    public async Task ExportThenImport_ReloadsSitesAndReportsResults()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));
        _filePicker.Path = Path.Combine(_directory.Path, "export.opml");

        await _viewModel.ExportOpmlCommand.ExecuteAsync(null);
        await _viewModel.ImportJsonCommand.ExecuteAsync(null);

        Assert.AreEqual("Exported: 0\nNot exported (no feed URL): 1", _dialogs.Messages[0]);
        Assert.HasCount(1, _dialogs.Errors);

        _filePicker.Path = Path.Combine(_directory.Path, "export.json");
        await _viewModel.ExportJsonCommand.ExecuteAsync(null);
        _dialogs.SelectedImportMode = ImportMode.Replace;
        await _viewModel.ImportJsonCommand.ExecuteAsync(null);

        Assert.AreEqual(
            "Added: 1\nSkipped (already registered): 0\nSkipped (invalid URL): 0",
            _dialogs.Messages.Last());
        Assert.AreEqual("A", _viewModel.Sites.Single().Title);
    }

    [TestMethod]
    public async Task Import_DoesNothingWhenModeIsCancelled()
    {
        _filePicker.Path = Path.Combine(_directory.Path, "missing.json");
        _dialogs.SelectedImportMode = null;

        await _viewModel.ImportJsonCommand.ExecuteAsync(null);

        Assert.IsEmpty(_dialogs.Messages);
        Assert.IsEmpty(_dialogs.Errors);
    }

    private MainViewModel CreateViewModel() => new(
        _service,
        _transferService,
        new SettingsService(Path.Combine(_directory.Path, "settings.json"), NullLogger<SettingsService>.Instance),
        _browser,
        _filePicker,
        _dialogs,
        NullLogger<MainViewModel>.Instance);

    // Registered in the order b, C, a. Visited: a (newest), b, C (never). Previewed: C (newest), b, a (never).
    private async Task AddSitesAsync()
    {
        int b = (await _service.AddAsync(new SiteInput("b", "https://b.invalid/", null))).Id;
        int c = (await _service.AddAsync(new SiteInput("C", "https://c.invalid/", null))).Id;
        int a = (await _service.AddAsync(new SiteInput("a", "https://a.invalid/", null))).Id;
        await MarkAsync(b, a, visited: true);
        await MarkAsync(b, c, visited: false);
    }

    // Marks the first site, then the second one a day later.
    private async Task MarkAsync(int older, int newer, bool visited)
    {
        foreach ((int id, int days) in new[] { (older, 1), (newer, 2) })
        {
            SiteService service = new(_database.Factory, new FixedTimeProvider(s_now.AddDays(days)), NullLogger<SiteService>.Instance);
            _ = visited ? await service.MarkVisitedAsync(id) : await service.MarkPreviewedAsync(id);
        }
    }

    private string[] Titles() => _viewModel.Sites.Select(s => s.Title).ToArray();

    private int[] Numbers() => _viewModel.Sites.Select(s => s.Number).ToArray();

    private async Task<SiteItemViewModel> AddAndLoadAsync()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.invalid/", null));
        await _viewModel.LoadAsync();
        return _viewModel.Sites.Single();
    }

    private static bool Fill(SiteEditorViewModel editor, string title, string url)
    {
        editor.Title = title;
        editor.Url = url;
        return true;
    }
}
