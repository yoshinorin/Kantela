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
        BookmarkTransferService transferService = new(
            _database.Factory,
            new BackupService(_database.Factory, _directory.Path, TimeProvider.System, NullLogger<BackupService>.Instance),
            TimeProvider.System,
            NullLogger<BookmarkTransferService>.Instance);
        _viewModel = new MainViewModel(
            _service, transferService, _browser, _filePicker, _dialogs, NullLogger<MainViewModel>.Instance);
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
        await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
        await _service.AddAsync(new SiteInput("B", "https://b.example/", null));

        await _viewModel.LoadAsync();

        CollectionAssert.AreEqual(new[] { "A", "B" }, _viewModel.Sites.Select(s => s.Title).ToArray());
    }

    [TestMethod]
    public async Task Open_LaunchesBrowserAndRecordsVisit()
    {
        SiteItemViewModel site = await AddAndLoadAsync();

        await _viewModel.OpenCommand.ExecuteAsync(site);

        Assert.AreEqual(new Uri("https://a.example/"), _browser.OpenedUris.Single());
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
        _dialogs.EditorResponses.Enqueue(editor => Fill(editor, "Dup", "https://a.example/"));
        _dialogs.EditorResponses.Enqueue(editor => Fill(editor, "New", "https://b.example/"));

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
        _dialogs.EditorResponses.Enqueue(editor => Fill(editor, "Renamed", "https://a.example/"));

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
    public async Task SaveOrderAsync_PersistsCollectionOrder()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
        await _service.AddAsync(new SiteInput("B", "https://b.example/", null));
        await _viewModel.LoadAsync();

        _viewModel.Sites.Move(1, 0);
        await _viewModel.SaveOrderAsync();

        CollectionAssert.AreEqual(new[] { "B", "A" }, (await _service.GetAllAsync()).Select(s => s.Title).ToArray());
    }

    [TestMethod]
    public async Task ExportThenImport_ReloadsSitesAndReportsResults()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
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

    private async Task<SiteItemViewModel> AddAndLoadAsync()
    {
        await _service.AddAsync(new SiteInput("A", "https://a.example/", null));
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
