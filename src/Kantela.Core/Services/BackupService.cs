using Kantela.Core.Data;
using Kantela.Core.Models;
using Kantela.Core.Services.Transfer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services;

public sealed class BackupService(
    IDbContextFactory<KantelaDbContext> dbContextFactory,
    string backupDirectory,
    TimeProvider timeProvider,
    ILogger<BackupService> logger,
    int retention = 5)
{
    private const string FilePrefix = "kantela-";
    private readonly Lock _lock = new();

    // Synchronous so that it can run from exit and crash handlers.
    public string? CreateBackup()
    {
        lock (_lock)
        {
            using KantelaDbContext db = dbContextFactory.CreateDbContext();
            List<Site> sites = db.Sites.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToList();
            if (sites.Count == 0)
            {
                logger.LogInformation("Skipped backup because there are no sites");
                return null;
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            Directory.CreateDirectory(backupDirectory);
            string path = Path.Combine(backupDirectory, $"{FilePrefix}{now.ToLocalTime():yyyyMMdd-HHmmss}.json");
            string temporaryPath = path + ".tmp";

            using (FileStream stream = File.Create(temporaryPath))
            {
                JsonBookmarkFormat.Write(stream, sites, now);
            }

            File.Move(temporaryPath, path, overwrite: true);
            logger.LogInformation("Created backup {Path}", path);

            DeleteOldBackups();
            return path;
        }
    }

    private void DeleteOldBackups()
    {
        IEnumerable<string> oldBackups = Directory.GetFiles(backupDirectory, $"{FilePrefix}*.json")
            .OrderByDescending(p => Path.GetFileName(p), StringComparer.Ordinal)
            .Skip(retention);
        foreach (string path in oldBackups)
        {
            File.Delete(path);
            logger.LogInformation("Deleted old backup {Path}", path);
        }
    }
}
