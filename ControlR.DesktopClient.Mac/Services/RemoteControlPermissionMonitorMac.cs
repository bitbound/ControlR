using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Mac.Services;
using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.DesktopClient.ViewModels.Mac;
using ControlR.Libraries.Hosting;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Logging;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class RemoteControlPermissionMonitorMac(
  TimeProvider timeProvider,
  IMacInterop macInterop,
  IUiThread uiThread,
  IToaster toaster,
  INavigationProvider navigationProvider,
  ILogger<RemoteControlPermissionMonitorMac> logger)
  : PeriodicBackgroundService(
      TimeSpan.FromMinutes(10),
      timeProvider,
      logger)
{
  private readonly IMacInterop _macInterop = macInterop;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IToaster _toaster = toaster;
  private readonly IUiThread _uiThread = uiThread;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      using var dedupeLogger = Logger.EnterDedupeScope();
      Logger.LogInformation("Starting macOS permission monitoring service");
      await CheckPermissions(dedupeLogger);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error starting RemoteControlPermissionMonitor.");
    }

    await base.ExecuteAsync(stoppingToken);
  }

  protected override async Task HandleElapsed()
  {
    await CheckPermissions(DedupeLogger);
  }

  private async Task CheckPermissions(LogDeduplicationContext<PeriodicBackgroundService> dedupeLogger)
  {
    try
    {
      dedupeLogger.LogInformationDeduped("Checking macOS remote control permissions");

      var isAccessibilityGranted = _macInterop.IsMacAccessibilityPermissionGranted();
      var isScreenCaptureGranted = _macInterop.IsMacScreenCapturePermissionGranted();
      var arePermissionsGranted = isAccessibilityGranted && isScreenCaptureGranted;

      dedupeLogger.LogInformationDeduped(
        "macOS permissions: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
        args: [isAccessibilityGranted, isScreenCaptureGranted]);

      if (arePermissionsGranted)
      {
        dedupeLogger.LogInformationDeduped("All required permissions are granted");
        return;
      }

      dedupeLogger.LogWarningDeduped("Required permissions are missing");
      await ShowPermissionsMissingToast<IPermissionsViewModelMac>(dedupeLogger);
    }
    catch (Exception ex)
    {
      dedupeLogger.LogErrorDeduped("Error while checking permissions", exception: ex);
    }
  }

  private async Task ShowPermissionsMissingToast<TViewModel>(
    LogDeduplicationContext<PeriodicBackgroundService> dedupeLogger)
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
      dedupeLogger.LogErrorDeduped("Error while showing permissions missing toast", exception: ex);
    }
  }
}
