namespace Kantela.Core.Models;

public enum SiteSortColumn
{
    Added,
    Title,
    Previewed,
    Visited,
}

public sealed record SiteSort(SiteSortColumn Column = SiteSortColumn.Added, bool Descending = false)
{
    // Dates start from the newest; names and registration order start from the top.
    public static bool DefaultDescending(SiteSortColumn column) =>
        column is SiteSortColumn.Previewed or SiteSortColumn.Visited;
}
