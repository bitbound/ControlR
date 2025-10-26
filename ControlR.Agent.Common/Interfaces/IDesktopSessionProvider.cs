namespace ControlR.Agent.Common.Interfaces;
public interface IDesktopSessionProvider
{
  Task<DesktopSession[]> GetActiveDesktopClients();
  Task<string[]> GetLoggedInUsers();
}
