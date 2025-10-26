namespace ControlR.Web.Client.Authz;

public static class UserClaimTypes
{
  public const string AuthenticationMethod = "controlr:auth:method";
  // New explicit claim indicating that the authenticated session is restricted to ONLY this device.
  public const string DeviceSessionScope = "controlr:device:scope:id";
  public const string TenantId = "controlr:tenant:id";
  public const string UserId = "controlr:user:id";
}