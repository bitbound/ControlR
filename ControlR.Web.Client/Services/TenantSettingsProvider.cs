namespace ControlR.Web.Client.Services;

public interface ITenantSettingsProvider
{
  Task<bool> GetNotifyUserOnSessionStart();
  Task SetNotifyUserOnSessionStart(bool value);
}
