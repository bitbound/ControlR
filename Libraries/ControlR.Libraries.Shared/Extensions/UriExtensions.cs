namespace ControlR.Libraries.Shared.Extensions;

public static class UriExtensions
{
    public static string GetOrigin(this Uri uri)
    {
        return $"{uri.Scheme}://{uri.Authority}";
    }
}