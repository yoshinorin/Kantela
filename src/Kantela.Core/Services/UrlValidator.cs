namespace Kantela.Core.Services;

public static class UrlValidator
{
    public static bool IsWebUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
