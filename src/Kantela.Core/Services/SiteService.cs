using Kantela.Core.Data;
using Kantela.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services;

public sealed record SiteInput(string Title, string Url, string? FeedUrl);

public sealed class DuplicateSiteUrlException(string url)
    : Exception($"A site with the URL '{url}' already exists.")
{
    public string Url { get; } = url;
}

public sealed class SiteService(
    IDbContextFactory<KantelaDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<SiteService> logger)
{
    public async Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Sites
            .AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Site> AddAsync(SiteInput input, CancellationToken cancellationToken = default)
    {
        SiteInput normalized = Normalize(input);
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureUrlIsAvailableAsync(db, normalized.Url, null, cancellationToken);

        int? maxSortOrder = await db.Sites.MaxAsync(s => (int?)s.SortOrder, cancellationToken);
        Site site = new()
        {
            Title = normalized.Title,
            Url = normalized.Url,
            FeedUrl = normalized.FeedUrl,
            SortOrder = (maxSortOrder ?? -1) + 1,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Sites.Add(site);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Added site {SiteId}: {Url}", site.Id, site.Url);
        return site;
    }

    public async Task<Site> UpdateAsync(int id, SiteInput input, CancellationToken cancellationToken = default)
    {
        SiteInput normalized = Normalize(input);
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        Site site = await db.Sites.SingleAsync(s => s.Id == id, cancellationToken);
        await EnsureUrlIsAvailableAsync(db, normalized.Url, id, cancellationToken);

        site.Title = normalized.Title;
        site.Url = normalized.Url;
        site.FeedUrl = normalized.FeedUrl;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated site {SiteId}: {Url}", site.Id, site.Url);
        return site;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Sites.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Deleted site {SiteId}", id);
    }

    public async Task<DateTime> MarkVisitedAsync(int id, CancellationToken cancellationToken = default)
    {
        DateTime visitedAt = timeProvider.GetUtcNow().UtcDateTime;
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Sites
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastVisitedAt, visitedAt), cancellationToken);
        return visitedAt;
    }

    public async Task<DateTime> MarkPreviewedAsync(int id, CancellationToken cancellationToken = default)
    {
        DateTime previewedAt = timeProvider.GetUtcNow().UtcDateTime;
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Sites
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastPreviewedAt, previewedAt), cancellationToken);
        return previewedAt;
    }

    // Sites missing from orderedIds keep their relative order after the listed ones.
    public async Task ReorderAsync(IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<Site> sites = await db.Sites
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        Dictionary<int, int> positions = orderedIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        List<Site> reordered = sites
            .OrderBy(s => positions.TryGetValue(s.Id, out int position) ? position : int.MaxValue)
            .ToList();

        for (int i = 0; i < reordered.Count; i++)
        {
            reordered[i].SortOrder = i;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static SiteInput Normalize(SiteInput input)
    {
        string title = input.Title.Trim();
        string url = input.Url.Trim();
        string? feedUrl = string.IsNullOrWhiteSpace(input.FeedUrl) ? null : input.FeedUrl.Trim();

        if (title.Length == 0)
        {
            throw new ArgumentException("Title is required.", nameof(input));
        }

        if (!UrlValidator.IsWebUrl(url))
        {
            throw new ArgumentException($"'{url}' is not a valid web URL.", nameof(input));
        }

        if (feedUrl is not null && !UrlValidator.IsWebUrl(feedUrl))
        {
            throw new ArgumentException($"'{feedUrl}' is not a valid web URL.", nameof(input));
        }

        return new SiteInput(title, url, feedUrl);
    }

    private static async Task EnsureUrlIsAvailableAsync(
        KantelaDbContext db, string url, int? excludedId, CancellationToken cancellationToken)
    {
        bool exists = await db.Sites.AnyAsync(s => s.Url == url && s.Id != excludedId, cancellationToken);
        if (exists)
        {
            throw new DuplicateSiteUrlException(url);
        }
    }
}
