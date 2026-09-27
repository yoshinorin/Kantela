using Kantela.Core.Services;
using Kantela.Core.Services.Transfer;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kantela.Core.Tests.Services;

[TestClass]
public sealed class BackupServiceTests
{
    private TestDatabase _database = null!;
    private TestDirectory _directory = null!;
    private FixedTimeProvider _timeProvider = null!;
    private BackupService _backupService = null!;

    [TestInitialize]
    public void Initialize()
    {
        _database = new TestDatabase();
        _directory = new TestDirectory();
        _timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero));
        _backupService = new BackupService(
            _database.Factory, _directory.Path, _timeProvider, NullLogger<BackupService>.Instance, retention: 5);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _database.Dispose();
        _directory.Dispose();
    }

    [TestMethod]
    public async Task CreateBackup_WritesJsonExport()
    {
        await AddSiteAsync();

        string? path = _backupService.CreateBackup();

        Assert.IsNotNull(path);
        await using FileStream stream = File.OpenRead(path);
        Assert.AreEqual("https://a.invalid/", JsonBookmarkFormat.Read(stream).Single().Url);
        Assert.IsEmpty(Directory.GetFiles(_directory.Path, "*.tmp"));
    }

    [TestMethod]
    public void CreateBackup_SkipsWhenThereAreNoSites()
    {
        Assert.IsNull(_backupService.CreateBackup());
        Assert.IsEmpty(Directory.GetFiles(_directory.Path));
    }

    [TestMethod]
    public async Task CreateBackup_KeepsNewestFiveBackups()
    {
        await AddSiteAsync();
        List<string> paths = [];
        for (int i = 0; i < 7; i++)
        {
            paths.Add(_backupService.CreateBackup()!);
            _timeProvider.Now = _timeProvider.Now.AddMinutes(1);
        }

        CollectionAssert.AreEquivalent(
            paths.Skip(2).ToArray(),
            Directory.GetFiles(_directory.Path));
    }

    private Task AddSiteAsync() =>
        new SiteService(_database.Factory, _timeProvider, NullLogger<SiteService>.Instance)
            .AddAsync(new SiteInput("A", "https://a.invalid/", null));
}
