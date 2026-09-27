namespace Kantela.Core.Models;

// Cached favicon of a site, deleted together with the site. It can be fetched again at any time,
// so it is not included in backups or exports.
public class SiteIcon
{
    public int SiteId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public DateTime FetchedAt { get; set; }
}

public sealed record FaviconImage(string ContentType, byte[] Data)
{
    public bool IsSvg => ContentType == "image/svg+xml";
}
