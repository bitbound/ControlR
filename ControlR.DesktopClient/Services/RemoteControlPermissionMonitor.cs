using Avalonia.Threading;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.ViewModels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

/// <summary>
/// Background service that periodically checks for OS-level remote control permissions
/// on macOS and Linux Wayland systems. Notifies the user if permissions are missing.
/// </summary>
public class RemoteControlPermissionMonitor(
  TimeProvider timeProvider,
  IServiceProvider serviceProvider,
  IToaster toaster,
  INavigationProvider navigationProvider,
  ILogger<RemoteControlPermissionMonitor> logger) : BackgroundService
{
  private readonly ILogger<RemoteControlPermissionMonitor> _logger = logger;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IToaster _toaster = toaster;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // Only run on macOS or Linux Wayland
    if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
    {
      _logger.LogInformation("Permission monitoring is not required on this platform");
      return;
    }

    _logger.LogInformation("Starting permission monitoring service");

    using var timer = _timeProvider.CreateTimer(
      CheckPermissions,
      state: null,
      dueTime: TimeSpan.Zero, // Check immediately on startup
      period: TimeSpan.FromMinutes(10)); // Then check every 10 minutes

    // Keep the service running until cancellation is requested
    await Task.Delay(Timeout.Infinite, stoppingToken);
  }

  private async void CheckPermissions(object? state)
  {
    try
    {
      _logger.LogInformation("Checking OS remote control permissions");

      var arePermissionsGranted = false;
      if (OperatingSystem.IsMacOS())
      {
        var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
        var isAccessibilityGranted = macInterop.IsAccessibilityPermissionGranted();
        var isScreenCaptureGranted = macInterop.IsScreenCapturePermissionGranted();
        arePermissionsGranted = isAccessibilityGranted && isScreenCaptureGranted;

        _logger.LogInformation(
          "macOS permissions: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
          isAccessibilityGranted,
          isScreenCaptureGranted);
      }
      else if (OperatingSystem.IsLinux())
      {
        var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
        if (detector.IsWayland())
        {
          var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
          arePermissionsGranted = await waylandPermissions.IsRemoteControlPermissionGranted();

          _logger.LogInformation("Wayland permissions: RemoteControl={RemoteControl}", arePermissionsGranted);
        }
        else
        {
          // X11 doesn't require special permissions
          _logger.LogInformation("X11 detected, no permission monitoring required");
          return;
        }
      }

      if (!arePermissionsGranted)
      {
        _logger.LogWarning("Required permissions are missing");
        await ShowPermissionsMissingToast();
      }
      else
      {
        _logger.LogInformation("All required permissions are granted");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking permissions");
    }
  }

  private async Task ShowPermissionsMissingToast()
  {
    try
    {
      await Dispatcher.UIThread.InvokeAsync(async () =>
      {
        await _toaster.ShowToast(
          Localization.PermissionsMissingToastTitle,
          Localization.PermissionsMissingToastMessage,
          ToastIcon.Warning,
          async () =>
          {
            // When clicked, show the main window and navigate to ManagedDeviceView
            await _navigationProvider.ShowMainWindowAndNavigateTo<IManagedDeviceViewModel>();
          });
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while showing permissions missing toast");
    }
  }
}
