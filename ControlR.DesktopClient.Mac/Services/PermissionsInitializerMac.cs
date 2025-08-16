using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

[SupportedOSPlatform("macos")]
public class PermissionsInitializerMac(
  TimeProvider timeProvider,
  IMacInterop macInterop,
  ILogger<PermissionsInitializerMac> logger) : BackgroundService
{
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IMacInterop _macInterop = macInterop;
  private readonly ILogger<PermissionsInitializerMac> _logger = logger;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      // Wait a bit to let the application initialize
      await Task.Delay(TimeSpan.FromSeconds(3), _timeProvider, stoppingToken);

      // Check and request Accessibility permission
      await RequestAccessibilityPermission(stoppingToken);

      // Check and request Screen Capture permission
      await RequestScreenCapturePermission(stoppingToken);
    }
    catch (OperationCanceledException)
    {
      // Expected when the service is being stopped
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in permissions requester service.");
    }
  }

  private async Task RequestAccessibilityPermission(CancellationToken cancellationToken)
  {
    try
    {
      if (_macInterop.IsAccessibilityPermissionGranted())
      {
        _logger.LogInformation("Accessibility permission is already granted.");
        return;
      }

      _logger.LogInformation("Requesting accessibility permission.");
      _macInterop.RequestAccessibilityPermission();

      var sw = Stopwatch.StartNew();
      while (!_macInterop.IsAccessibilityPermissionGranted())
      {
        await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, cancellationToken);
        if (sw.Elapsed > TimeSpan.FromMinutes(10))
        {
          _macInterop.RequestAccessibilityPermission();
          sw.Restart();
        }
      }

      _logger.LogInformation("Accessibility permissions granted.");
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Permission request cancelled.  Application shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error requesting accessibility permission.");
    }
  }

  private async Task RequestScreenCapturePermission(CancellationToken cancellationToken)
  {
    try
    {
      if (_macInterop.IsScreenCapturePermissionGranted())
      {
        _logger.LogInformation("Screen capture permission is already granted.");
        return;
      }

      _logger.LogInformation("Requesting screen capture permission.");
      _macInterop.RequestScreenCapturePermission();

      var sw = Stopwatch.StartNew();
      while (!_macInterop.IsScreenCapturePermissionGranted())
      {
        await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, cancellationToken);
        if (sw.Elapsed > TimeSpan.FromMinutes(10))
        {
          _macInterop.RequestScreenCapturePermission();
          sw.Restart();
        }
      }

      _logger.LogInformation("Screen capture permissions granted.");
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Screen capture permission request cancelled.  Application shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error requesting screen capture permission.");
    }
  }
}