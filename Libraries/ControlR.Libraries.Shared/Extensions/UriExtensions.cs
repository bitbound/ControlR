namespace ControlR.Libraries.Shared.Extensions;

public static class UriExtensions
{
    public static string GetOrigin(this Uri uri)
    {
        return $"{uri.Scheme}://{uri.Authority}";
    }

    public static bool IsHttp(this Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    public static Uri ToWebsocketUri(this Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Only http and https schemes are supported.");
        }

        var scheme = uri.Scheme.StartsWith("https", StringComparison.CurrentCulture) ? "wss" : "ws";
        return new Uri($"{scheme}://{uri.Authority}{uri.PathAndQuery}");
    }
}