using ControlR.Libraries.Shared.Logging;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.DesktopClient.ViewModels.Linux;
using ControlR.Libraries.Avalonia.Services;
using ControlR.Libraries.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

public class RemoteControlPermissionMonitorWayland(
  TimeProvider timeProvider,
  IDesktopEnvironmentDetector desktopEnvironmentDetector,
  INavigationProvider navigationProvider,
  IToaster toaster,
  IUiDispatcher dispatcher,
  IWaylandPermissionProvider waylandPermissionProvider,
  ILogger<RemoteControlPermissionMonitorWayland> logger)
  : PeriodicBackgroundService(
      period: TimeSpan.FromMinutes(10),
      catchExceptions: true,
      timeProvider,
      logger)
{
  private readonly IDesktopEnvironmentDetector _desktopEnvironmentDetector = desktopEnvironmentDetector;
  private readonly IUiDispatcher _dispatcher = dispatcher;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly IToaster _toaster = toaster;
  private readonly IWaylandPermissionProvider _waylandPermissionProvider = waylandPermissionProvider;

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
      Logger.LogInformationDeduped("Checking Wayland restore token");

      var hasToken = _waylandPermissionProvider.HasRestoreToken();

      if (hasToken)
      {
        Logger.LogInformationDeduped("Wayland restore token exists");
        return;
      }

      Logger.LogWarningDeduped("Wayland restore token is missing");
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
      await _dispatcher.InvokeAsync(async () =>
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
      Logger.LogErrorDeduped("Error while showing permissions missing toast", exception: ex);
    }
  }
}
