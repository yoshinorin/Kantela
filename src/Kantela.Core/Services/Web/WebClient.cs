using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Kantela.Core.Services.Web;

// A successfully fetched resource. FinalUri is the address after redirects. Content may be truncated.
public sealed record WebPage(Uri FinalUri, string? ContentType, byte[] Content);

public interface IWebClient
{
    // Returns null when the resource cannot be fetched (network error, timeout or non-success status).
    Task<WebPage?> GetAsync(Uri uri, CancellationToken cancellationToken = default);
}

public sealed class HttpWebClient(HttpClient httpClient, ILogger<HttpWebClient> logger) : IWebClient
{
    public const string RepositoryUrl = "https://github.com/yoshinorin/Kantela";

    // Enough for the <head> of a page and the root element of a feed.
    private const int MaxContentBytes = 2 * 1024 * 1024;

    public static HttpClient CreateHttpClient(string productVersion)
    {
        SocketsHttpHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kantela", productVersion));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"(+{RepositoryUrl})"));
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        return client;
    }

    public async Task<WebPage?> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Fetching {Url} returned {StatusCode}", uri, (int)response.StatusCode);
                return null;
            }

            byte[] content = await ReadAsync(response.Content, cancellationToken);
            return new WebPage(
                response.RequestMessage?.RequestUri ?? uri,
                response.Content.Headers.ContentType?.ToString(),
                content);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // TaskCanceledException without a cancellation request is the client timeout.
            logger.LogWarning(ex, "Failed to fetch {Url}", uri);
            return null;
        }
    }

    private static async Task<byte[]> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        int read;
        while (buffer.Length < MaxContentBytes
            && (read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, MaxContentBytes - buffer.Length)), cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
