using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Windows.Services;

/// <summary>
/// Responsible for any environment setup or teardown needed for remote control sessions.
/// </summary>
internal class RemoteControlEnvironmentService(IAeroPeekProvider aeroProvider) : IHostedService
{  
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _ = aeroProvider.SetAeroPeekEnabled(false);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _ = aeroProvider.SetAeroPeekEnabled(true);
    return Task.CompletedTask;
  }
}
