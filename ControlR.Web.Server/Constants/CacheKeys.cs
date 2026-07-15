namespace ControlR.Web.Server.Constants;

public static class CacheKeys
{
  public static string GetPersonalAccessTokenAuthFailure(string? remoteIp) => $"pat-auth-fail:{remoteIp ?? "(unknown IP)"}";
  public static string GetServiceAccountAuthFailure(string? remoteIp) => $"sa-auth-fail:{remoteIp ?? "(unknown IP)"}";
}
