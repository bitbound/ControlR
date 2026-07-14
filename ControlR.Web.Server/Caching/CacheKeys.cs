namespace ControlR.Web.Server.Caching;

public static class CacheKeys
{
  public static string GetServiceAccountAuthFailure(string? remoteIp) => $"sa-auth-fail:{remoteIp ?? "(unknown IP)"}";
}
