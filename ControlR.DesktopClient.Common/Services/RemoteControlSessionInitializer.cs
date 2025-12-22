using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Common.Services;

public class RemoteControlSessionInitializer(
  IDesktopRemoteControlStream remoteControlStream,
  IHostApplicationLifetime appLifetime,
  ILogger<RemoteControlSessionInitializer> logger) : BackgroundService
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly ILogger<RemoteControlSessionInitializer> _logger = logger;
  private readonly IDesktopRemoteControlStream _remoteControlStream = remoteControlStream;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Remote control session initializer started.");
    try
    {
      await _remoteControlStream.StreamScreen(stoppingToken);
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Remote control session initializer is stopping due to cancellation.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in remote control session initializer.");
    }
    finally
    {
      _appLifetime.StopApplication();
    }
  }
}