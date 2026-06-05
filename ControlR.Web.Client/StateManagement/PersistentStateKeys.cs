namespace ControlR.Web.Client.StateManagement;

/// <summary>
/// Keys for use with the <see cref="PersistentComponentState"/> service.
/// </summary>
public static class PersistentStateKeys
{
  public const string IsDarkMode = nameof(IsDarkMode);
  public const string ServerDecommissioned = nameof(ServerDecommissioned);
  public const string UserInfo = nameof(UserInfo);
}
