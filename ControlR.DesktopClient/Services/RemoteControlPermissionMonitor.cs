using Avalonia.Threading;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.ViewModels;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services;
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
  ISystemEnvironment systemEnvironment,
  IToaster toaster,
  INavigationProvider navigationProvider,
  ILogger<RemoteControlPermissionMonitor> logger) : BackgroundService
{
  private readonly ILogger<RemoteControlPermissionMonitor> _logger = logger;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IToaster _toaster = toaster;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // Only run on macOS or Linux Wayland
    if (!_systemEnvironment.IsMacOS() && !_systemEnvironment.IsLinux())
    {
      _logger.LogInformation("Permission monitoring is not required on this platform");
      return;
    }

    _logger.LogInformation("Starting permission monitoring service");
    await CheckPermissions();

    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10), _timeProvider);
    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        await CheckPermissions();
      }
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Application is shutting down. Stopping permission monitoring service.");
    }
  }

  private async Task CheckPermissions()
  {
    try
    {
      _logger.LogInformationDeduped("Checking OS remote control permissions");

      var arePermissionsGranted = false;
      if (_systemEnvironment.IsMacOS())
      {
        var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
        var isAccessibilityGranted = macInterop.IsMacAccessibilityPermissionGranted();
        var isScreenCaptureGranted = macInterop.IsMacScreenCapturePermissionGranted();
        arePermissionsGranted = isAccessibilityGranted && isScreenCaptureGranted;

        _logger.LogInformationDeduped(
          "macOS permissions: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
          args: (isAccessibilityGranted, isScreenCaptureGranted));
      }
      else if (_systemEnvironment.IsLinux())
      {
        var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
        if (detector.IsWayland())
        {
          var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
          arePermissionsGranted = await waylandPermissions.IsRemoteControlPermissionGranted();

          _logger.LogInformationDeduped("Wayland permissions: RemoteControl={RemoteControl}", args: arePermissionsGranted);
        }
        else
        {
          // X11 doesn't require special permissions
          _logger.LogInformationDeduped("X11 detected, no permission monitoring required");
          return;
        }
      }

      if (!arePermissionsGranted)
      {
        _logger.LogWarningDeduped("Required permissions are missing");
        await ShowPermissionsMissingToast();
      }
      else
      {
        _logger.LogInformationDeduped("All required permissions are granted");
      }
    }
    catch (Exception ex)
    {
      _logger.LogErrorDeduped("Error while checking permissions", exception: ex);
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
      _logger.LogErrorDeduped("Error while showing permissions missing toast", exception: ex);
    }
  }
}
