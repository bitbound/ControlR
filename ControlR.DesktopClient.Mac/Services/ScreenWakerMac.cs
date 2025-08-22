using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;
public class ScreenWakerMac(
  IMacInterop macInterop,
  ILogger<ScreenGrabberMac> logger) : IHostedService
{
  private readonly IMacInterop _macInterop = macInterop;
  private readonly ILogger<ScreenGrabberMac> _logger = logger;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    var result = _macInterop.WakeScreen();
    _logger.LogResult(result);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
}
