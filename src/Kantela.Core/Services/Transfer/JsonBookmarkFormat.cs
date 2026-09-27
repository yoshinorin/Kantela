using System.Text.Json;
using Kantela.Core.Models;

namespace Kantela.Core.Services.Transfer;

public static class JsonBookmarkFormat
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public static void Write(Stream stream, IReadOnlyList<Site> sites, DateTime exportedAt)
    {
        Document document = new(
            CurrentVersion,
            exportedAt,
            sites.Select(s => new DocumentSite(s.Title, s.Url, s.FeedUrl, s.CreatedAt, s.LastVisitedAt, s.LastPreviewedAt, s.Alias)).ToList());
        JsonSerializer.Serialize(stream, document, s_options);
    }

    public static IReadOnlyList<ImportedSite> Read(Stream stream)
    {
        Document document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(stream, s_options)
                ?? throw new InvalidDataException("The file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The file is not a valid Kantela JSON export: {ex.Message}", ex);
        }

        if (document.Version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported JSON export version: {document.Version}.");
        }

        // "sortOrder" in files exported by older versions is ignored; sites are imported in file order.
        return document.Sites
            .Select(s => new ImportedSite(s.Title, s.Url, s.FeedUrl, s.CreatedAt, s.LastVisitedAt, s.LastPreviewedAt, s.Alias))
            .ToList();
    }

    private sealed record Document(int Version, DateTime ExportedAt, List<DocumentSite> Sites);

    private sealed record DocumentSite(
        string Title,
        string Url,
        string? FeedUrl,
        DateTime CreatedAt,
        DateTime? LastVisitedAt,
        DateTime? LastPreviewedAt = null,
        string? Alias = null);
}
