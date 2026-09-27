using Kantela.Core.Data;
using Kantela.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services;

public sealed record SiteInput(string Title, string Url, string? FeedUrl, string? Alias = null);

public sealed class DuplicateSiteUrlException(string url)
    : SiteRegistrationException($"'{url}' is already registered.")
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
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Site> AddAsync(SiteInput input, CancellationToken cancellationToken = default)
    {
        SiteInput normalized = Normalize(input);
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureUrlIsAvailableAsync(db, normalized.Url, null, cancellationToken);

        Site site = new()
        {
            Title = normalized.Title,
            Alias = normalized.Alias,
            Url = normalized.Url,
            FeedUrl = normalized.FeedUrl,
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
        site.Alias = normalized.Alias;
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

    // Throws DuplicateSiteUrlException when another site has the same URL after normalization (see UrlNormalizer).
    public async Task EnsureUrlIsAvailableAsync(string url, int? excludedId = null, CancellationToken cancellationToken = default)
    {
        await using KantelaDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureUrlIsAvailableAsync(db, url.Trim(), excludedId, cancellationToken);
    }

    private static SiteInput Normalize(SiteInput input)
    {
        string title = input.Title.Trim();
        string url = input.Url.Trim();
        string? feedUrl = string.IsNullOrWhiteSpace(input.FeedUrl) ? null : input.FeedUrl.Trim();
        string? alias = string.IsNullOrWhiteSpace(input.Alias) ? null : input.Alias.Trim();

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

        return new SiteInput(title, url, feedUrl, alias);
    }

    // Compared in memory because the normalized form is not stored; the number of sites is small.
    private static async Task EnsureUrlIsAvailableAsync(
        KantelaDbContext db, string url, int? excludedId, CancellationToken cancellationToken)
    {
        string key = UrlNormalizer.ComparisonKey(url);
        List<string> otherUrls = await db.Sites
            .Where(s => s.Id != excludedId)
            .Select(s => s.Url)
            .ToListAsync(cancellationToken);
        if (otherUrls.Any(u => UrlNormalizer.ComparisonKey(u) == key))
        {
            throw new DuplicateSiteUrlException(url);
        }
    }
}
