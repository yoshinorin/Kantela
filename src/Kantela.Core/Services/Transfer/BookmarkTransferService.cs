using Kantela.Core.Data;
using Kantela.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services.Transfer;

public sealed class BookmarkTransferService(
    IDbContextFactory<KantelaDbContext> dbContextFactory,
    BackupService backupService,
    TimeProvider timeProvider,
    ILogger<BookmarkTransferService> logger)
{
    public async Task<ExportResult> ExportAsync(
        BookmarkFormat format, string path, CancellationToken cancellationToken = default)
    {
        List<Site> sites;
        await using (KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            sites = await db.Sites
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        await using FileStream stream = File.Create(path);
        int skipped = format switch
        {
            BookmarkFormat.Json => WriteJson(stream, sites, now),
            BookmarkFormat.Opml => OpmlBookmarkFormat.Write(stream, sites, now, logger),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        logger.LogInformation("Exported {Count} sites as {Format} to {Path}", sites.Count - skipped, format, path);
        return new ExportResult(sites.Count - skipped, skipped);
    }

    public async Task<ImportResult> ImportAsync(
        BookmarkFormat format, string path, ImportMode mode, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ImportedSite> sites;
        await using (FileStream stream = File.OpenRead(path))
        {
            sites = format switch
            {
                BookmarkFormat.Json => JsonBookmarkFormat.Read(stream),
                BookmarkFormat.Opml => OpmlBookmarkFormat.Read(stream, logger),
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };
        }

        return await ImportAsync(sites, mode, cancellationToken);
    }

    public async Task<ImportResult> ImportAsync(
        IReadOnlyList<ImportedSite> sites, ImportMode mode, CancellationToken cancellationToken = default)
    {
        if (mode == ImportMode.Replace)
        {
            backupService.CreateBackup();
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (mode == ImportMode.Replace)
        {
            await db.Sites.ExecuteDeleteAsync(cancellationToken);
        }

        HashSet<string> knownUrls = new(await db.Sites.Select(s => s.Url).ToListAsync(cancellationToken), StringComparer.Ordinal);
        int added = 0;
        int skippedDuplicates = 0;
        int skippedInvalid = 0;

        foreach (ImportedSite imported in sites)
        {
            string url = imported.Url.Trim();
            if (!UrlValidator.IsWebUrl(url))
            {
                logger.LogWarning("Skipped import of {Url} because it is not a valid web URL", url);
                skippedInvalid++;
                continue;
            }

            if (!knownUrls.Add(url))
            {
                logger.LogInformation("Skipped import of {Url} because it is already registered", url);
                skippedDuplicates++;
                continue;
            }

            string? feedUrl = string.IsNullOrWhiteSpace(imported.FeedUrl) ? null : imported.FeedUrl.Trim();
            if (feedUrl is not null && !UrlValidator.IsWebUrl(feedUrl))
            {
                logger.LogWarning("Dropped invalid feed URL {FeedUrl} of {Url}", feedUrl, url);
                feedUrl = null;
            }

            string title = imported.Title.Trim();
            db.Sites.Add(new Site
            {
                Title = title.Length == 0 ? url : title,
                Url = url,
                FeedUrl = feedUrl,
                CreatedAt = imported.CreatedAt ?? now,
                LastVisitedAt = imported.LastVisitedAt,
                LastPreviewedAt = imported.LastPreviewedAt,
            });
            added++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Imported sites ({Mode}): added {Added}, skipped duplicates {Duplicates}, skipped invalid {Invalid}",
            mode, added, skippedDuplicates, skippedInvalid);
        return new ImportResult(added, skippedDuplicates, skippedInvalid);
    }

    private static int WriteJson(Stream stream, IReadOnlyList<Site> sites, DateTime exportedAt)
    {
        JsonBookmarkFormat.Write(stream, sites, exportedAt);
        return 0;
    }
}
