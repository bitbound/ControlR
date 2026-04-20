using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.DesktopClient.ViewModels.Linux;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

public class RemoteControlPermissionMonitorWayland(
  TimeProvider timeProvider,
  IToaster toaster,
  IDesktopClientPermissionService desktopClientPermissionService,
  IDesktopEnvironmentDetector desktopEnvironmentDetector,
  INavigationProvider navigationProvider,
  IUiThread uiThread,
  ILogger<RemoteControlPermissionMonitorWayland> logger)
  : PeriodicBackgroundService(
      period: TimeSpan.FromMinutes(10),
      catchExceptions: true,
      timeProvider,
      logger)
{
  private readonly IDesktopClientPermissionService _desktopClientPermissionService = desktopClientPermissionService;
  private readonly IDesktopEnvironmentDetector _desktopEnvironmentDetector = desktopEnvironmentDetector;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IToaster _toaster = toaster;
  private readonly IUiThread _uiThread = uiThread;

  protected override async Task HandleElapsed()
  {
    if (!_desktopEnvironmentDetector.IsWayland())
    {
      return;
    }

    await CheckPermissions();
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    if (!_desktopEnvironmentDetector.IsWayland())
    {
      Logger.LogInformation("Permission monitoring is not required on this platform");
      return;
    }

    Logger.LogInformation("Starting Wayland permission monitoring service");
    await CheckPermissions();
  }

  private async Task CheckPermissions()
  {
    try
    {
      DedupeLogger.LogInformationDeduped("Checking Wayland remote control permissions");

      var permissionState = await _desktopClientPermissionService.GetPermissionState(DesktopClientPermissionScope.RemoteControl);
      var arePermissionsGranted = permissionState.ArePermissionsGranted;

      DedupeLogger.LogInformationDeduped(
        "Wayland permissions: RemoteControl={RemoteControl}",
        args: [arePermissionsGranted]);

      if (arePermissionsGranted)
      {
        DedupeLogger.LogInformationDeduped("All required permissions are granted");
        return;
      }

      DedupeLogger.LogWarningDeduped("Required permissions are missing");
      await ShowPermissionsMissingToast<IPermissionsViewModelWayland>();
    }
    catch (Exception ex)
    {
      DedupeLogger.LogErrorDeduped("Error while checking permissions", exception: ex);
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
            await _navigationProvider.ShowMainWindowAndNavigateTo<TViewModel>();
          });
      });
    }
    catch (Exception ex)
    {
      DedupeLogger.LogErrorDeduped("Error while showing permissions missing toast", exception: ex);
    }
  }
}
