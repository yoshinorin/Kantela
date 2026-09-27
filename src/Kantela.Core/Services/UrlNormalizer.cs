namespace Kantela.Core.Services;

public static class UrlNormalizer
{
    // A key that is equal for URLs of the same site written differently: the scheme (http/https),
    // host case, default port, fragment and trailing slashes are ignored. The path and query are kept.
    public static string ComparisonKey(string url)
    {
        Uri uri = new(url.Trim(), UriKind.Absolute);
        string port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.IdnHost.ToLowerInvariant()}{port}{uri.AbsolutePath.TrimEnd('/')}{uri.Query}";
    }
}
