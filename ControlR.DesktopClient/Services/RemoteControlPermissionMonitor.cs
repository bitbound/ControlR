using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.ViewModels.Linux;
using ControlR.DesktopClient.ViewModels.Mac;
using ControlR.Libraries.Hosting;
using ControlR.Libraries.Shared.Services;
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
  ILogger<RemoteControlPermissionMonitor> logger) 
  : PeriodicBackgroundService(
      TimeSpan.FromMinutes(10), 
      timeProvider, 
      logger)
{
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IToaster _toaster = toaster;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      // Only run on macOS or Linux Wayland
      if (!_systemEnvironment.IsMacOS() && !_systemEnvironment.IsLinux())
      {
        Logger.LogInformation("Permission monitoring is not required on this platform");
        return;
      }

      if (_systemEnvironment.IsLinux())
      {
        var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
        if (!detector.IsWayland())
        {
          Logger.LogInformation("Permission monitoring is not required on this platform");
          return;
        }
      }

      Logger.LogInformation("Starting permission monitoring service");
      await CheckPermissions();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error starting RemoteControlPermissionMonitor.");
    }

    await base.ExecuteAsync(stoppingToken);
  }

  protected override async Task HandleElapsed()
  {
    await CheckPermissions();
  }

  private async Task CheckPermissions()
  {
    try
    {
      Logger.LogInformationDeduped("Checking OS remote control permissions");

      if (_systemEnvironment.IsMacOS())
      {
        var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
        var isAccessibilityGranted = macInterop.IsMacAccessibilityPermissionGranted();
        var isScreenCaptureGranted = macInterop.IsMacScreenCapturePermissionGranted();
        var arePermissionsGranted = isAccessibilityGranted && isScreenCaptureGranted;

        Logger.LogInformationDeduped(
          "macOS permissions: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
          args: (isAccessibilityGranted, isScreenCaptureGranted));

        if (arePermissionsGranted)
        {
          Logger.LogInformationDeduped("All required permissions are granted");
          return;
        }
        Logger.LogWarningDeduped("Required permissions are missing");
        await ShowPermissionsMissingToast<IPermissionsViewModelMac>();
      }
      else if (_systemEnvironment.IsLinux())
      {
        var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
        if (detector.IsWayland())
        {
          var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
          var arePermissionsGranted = await waylandPermissions.IsRemoteControlPermissionGranted();

          Logger.LogInformationDeduped("Wayland permissions: RemoteControl={RemoteControl}", args: arePermissionsGranted);
          
          if (arePermissionsGranted)
          {
            Logger.LogInformationDeduped("All required permissions are granted");
            return;
          }
          Logger.LogWarningDeduped("Required permissions are missing");
          await ShowPermissionsMissingToast<IPermissionsViewModelWayland>();
        }
        else
        {
          // X11 doesn't require special permissions
          Logger.LogInformationDeduped("X11 detected, no permission monitoring required");
          return;
        }
      }
    }
    catch (Exception ex)
    {
      Logger.LogErrorDeduped("Error while checking permissions", exception: ex);
    }
  }

  private async Task ShowPermissionsMissingToast<TViewModel>()
    where TViewModel : IViewModelBase
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
            // When clicked, show the main window and navigate to Permissions.
            await _navigationProvider.ShowMainWindowAndNavigateTo<TViewModel>();
          });
      });
    }
    catch (Exception ex)
    {
      Logger.LogErrorDeduped("Error while showing permissions missing toast", exception: ex);
    }
  }
}
