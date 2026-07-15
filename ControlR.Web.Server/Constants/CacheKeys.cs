namespace ControlR.Web.Server.Constants;

public static class CacheKeys
{
  public static string GetServiceAccountAuthFailure(string? remoteIp) => $"sa-auth-fail:{remoteIp ?? "(unknown IP)"}";
}
