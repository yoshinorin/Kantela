namespace Kantela.Core.Services.Transfer;

public enum BookmarkFormat
{
    Json,
    Opml,
}

public enum ImportMode
{
    Merge,
    Replace,
}

public sealed record ImportedSite(
    string Title,
    string Url,
    string? FeedUrl,
    DateTime? CreatedAt = null,
    DateTime? LastVisitedAt = null);

public sealed record ImportResult(int Added, int SkippedDuplicates, int SkippedInvalid);

public sealed record ExportResult(int Exported, int Skipped);

public static class BookmarkFormatExtensions
{
    public static string DisplayName(this BookmarkFormat format) => format switch
    {
        BookmarkFormat.Json => "JSON",
        BookmarkFormat.Opml => "OPML",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static string DefaultExtension(this BookmarkFormat format) => format switch
    {
        BookmarkFormat.Json => ".json",
        BookmarkFormat.Opml => ".opml",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    public static IReadOnlyList<string> OpenExtensions(this BookmarkFormat format) => format switch
    {
        BookmarkFormat.Json => [".json"],
        BookmarkFormat.Opml => [".opml", ".xml"],
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
