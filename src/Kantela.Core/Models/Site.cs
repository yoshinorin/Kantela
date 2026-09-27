namespace Kantela.Core.Models;

public class Site
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public required string Url { get; set; }

    public string? FeedUrl { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastVisitedAt { get; set; }
}
