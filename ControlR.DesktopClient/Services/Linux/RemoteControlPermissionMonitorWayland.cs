#if IS_LINUX
using ControlR.Libraries.Hosting;
using Microsoft.Extensions.Logging;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Extensions;
using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.DesktopClient.Common;

namespace ControlR.DesktopClient.Services.Linux;

/// <summary>
/// Background service that periodically checks for Linux Wayland remote control permissions.
/// Notifies the user if permissions are missing.
/// </summary>
public class RemoteControlPermissionMonitorWayland(
  TimeProvider timeProvider,
  IToaster toaster,
  IDesktopEnvironmentDetector desktopEnvironmentDetector,
  INavigationProvider navigationProvider,
  IWaylandPermissionProvider waylandPermissionProvider,
  IUiThread uiThread,
  ILogger<RemoteControlPermissionMonitorWayland> logger)
  : PeriodicBackgroundService(
      TimeSpan.FromMinutes(10),
      timeProvider,
      logger)
{
  private readonly IDesktopEnvironmentDetector _desktopEnvironmentDetector = desktopEnvironmentDetector;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IToaster _toaster = toaster;
  private readonly IUiThread _uiThread = uiThread;
  private readonly IWaylandPermissionProvider _waylandPermissionProvider = waylandPermissionProvider;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      if (!_desktopEnvironmentDetector.IsWayland())
      {
        Logger.LogInformation("Permission monitoring is not required on this platform");
        return;
      }

      Logger.LogInformation("Starting Wayland permission monitoring service");
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
      Logger.LogInformationDeduped("Checking Wayland remote control permissions");

      var arePermissionsGranted = await _waylandPermissionProvider.IsRemoteControlPermissionGranted();

      Logger.LogInformationDeduped("Wayland permissions: RemoteControl={RemoteControl}", args: arePermissionsGranted);

      if (arePermissionsGranted)
      {
        Logger.LogInformationDeduped("All required permissions are granted");
        return;
      }

      Logger.LogWarningDeduped("Required permissions are missing");
      await ShowPermissionsMissingToast<IPermissionsViewModelWayland>();
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
      await _uiThread.InvokeAsync(async () =>
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
#endif