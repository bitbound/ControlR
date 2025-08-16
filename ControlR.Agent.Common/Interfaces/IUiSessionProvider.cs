namespace ControlR.Agent.Common.Interfaces;
public interface IUiSessionProvider
{
  Task<DeviceUiSession[]> GetActiveDesktopClients();
  Task<string[]> GetLoggedInUsers();
}
