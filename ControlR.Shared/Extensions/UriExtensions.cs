namespace ControlR.Shared.Extensions;

public static class UriExtensions
{
    public static string GetOrigin(this Uri uri)
    {
        return $"{uri.Scheme}://{uri.Authority}";
    }
}