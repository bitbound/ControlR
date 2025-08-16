using ControlR.Agent.Common.Interfaces;

namespace ControlR.Agent.Common.Services.Linux;
internal class UiSessionProviderLinux : IUiSessionProvider
{
  public Task<DeviceUiSession[]> GetActiveDesktopClients()
  {
    throw new NotImplementedException();
  }

  public Task<string[]> GetLoggedInUsers()
  {
    throw new NotImplementedException();
  }
}
