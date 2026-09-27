using System.Text.Json;
using System.Text.Json.Serialization;
using Kantela.Core.Models;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services;

public sealed class AppSettings
{
    public SiteSort Sort { get; init; } = new();
}

public sealed class SettingsService(string path, ILogger<SettingsService> logger)
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // A missing or unreadable file falls back to the defaults.
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<AppSettings>(stream, s_options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Failed to read settings from {Path}; using defaults", path);
            return new AppSettings();
        }
    }

    // Synchronous: the file is tiny, and overlapping asynchronous writes would collide on the temporary file.
    public void Save(AppSettings settings)
    {
        string temporaryPath = path + ".tmp";
        using (FileStream stream = File.Create(temporaryPath))
        {
            JsonSerializer.Serialize(stream, settings, s_options);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }
}
