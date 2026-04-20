using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.DesktopClient.ViewModels.Mac;
using ControlR.Libraries.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class RemoteControlPermissionMonitorMac(
  TimeProvider timeProvider,
  IDesktopClientPermissionService desktopClientPermissionService,
  IUiThread uiThread,
  IToaster toaster,
  INavigationProvider navigationProvider,
  ILogger<RemoteControlPermissionMonitorMac> logger)
  : PeriodicBackgroundService(
      period: TimeSpan.FromMinutes(10),
      catchExceptions: true,
      timeProvider,
      logger)
{
  private readonly IDesktopClientPermissionService _desktopClientPermissionService = desktopClientPermissionService;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IToaster _toaster = toaster;
  private readonly IUiThread _uiThread = uiThread;

  protected override async Task HandleElapsed()
  {
    await CheckPermissions();
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    Logger.LogInformation("Starting macOS permission monitoring service");
    await CheckPermissions();
  }

  private async Task CheckPermissions()
  {
    try
    {
      DedupeLogger.LogInformationDeduped("Checking macOS remote control permissions");

      var platformState = await _desktopClientPermissionService.GetPlatformPermissionState();
      var isAccessibilityGranted = platformState.IsAccessibilityGranted == true;
      var isScreenCaptureGranted = platformState.IsScreenCaptureGranted == true;
      var arePermissionsGranted = isAccessibilityGranted && isScreenCaptureGranted;

      DedupeLogger.LogInformationDeduped(
        "macOS permissions: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
        args: [isAccessibilityGranted, isScreenCaptureGranted]);

      if (arePermissionsGranted)
      {
        DedupeLogger.LogInformationDeduped("All required permissions are granted");
        return;
      }

      DedupeLogger.LogWarningDeduped("Required permissions are missing");
      await ShowPermissionsMissingToast<IPermissionsViewModelMac>();
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
