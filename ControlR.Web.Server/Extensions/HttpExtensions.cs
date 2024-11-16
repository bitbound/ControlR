namespace ControlR.Web.Server.Extensions;

public static class HttpExtensions
{
  public static Uri ToOrigin(this HttpRequest request)
  {
    return new Uri($"{request.Scheme}://{request.Host}");
  }
}
