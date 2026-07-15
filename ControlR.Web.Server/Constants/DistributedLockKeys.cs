namespace ControlR.Web.Server.Constants;

public static class DistributedLockKeys
{
  public static string GetLogonTokenKey(string token) => $"logon-token:{token}";
}