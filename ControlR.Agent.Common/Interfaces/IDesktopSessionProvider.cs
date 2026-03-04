using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Agent.Common.Interfaces;
public interface IDesktopSessionProvider
{
  Task<DesktopSession[]> GetActiveDesktopClients();
  Task<string[]> GetLoggedInUsers();
}
