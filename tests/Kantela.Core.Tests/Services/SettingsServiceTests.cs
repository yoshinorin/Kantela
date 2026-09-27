using Kantela.Core.Models;
using Kantela.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kantela.Core.Tests.Services;

[TestClass]
public sealed class SettingsServiceTests
{
    private TestDirectory _directory = null!;
    private string _path = null!;
    private SettingsService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = new TestDirectory();
        _path = Path.Combine(_directory.Path, "settings.json");
        _service = new SettingsService(_path, NullLogger<SettingsService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _directory.Dispose();

    [TestMethod]
    public void Load_ReturnsDefaultsWhenFileIsMissing()
    {
        Assert.AreEqual(new SiteSort(), _service.Load().Sort);
    }

    [TestMethod]
    public void SaveThenLoad_PreservesSettings()
    {
        _service.Save(new AppSettings { Sort = new SiteSort(SiteSortColumn.Visited, Descending: true) });

        Assert.AreEqual(new SiteSort(SiteSortColumn.Visited, Descending: true), _service.Load().Sort);
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("""{ "sort": null }""")]
    [DataRow("""{ "sort": { "column": "Unknown", "descending": true } }""")]
    public void Load_ReturnsDefaultsWhenFileIsInvalid(string json)
    {
        File.WriteAllText(_path, json);

        Assert.AreEqual(new SiteSort(), _service.Load().Sort);
    }
}
