using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

internal class DesktopClientPermissionServiceLinux(
  IServiceProvider serviceProvider,
  ILogger<DesktopClientPermissionServiceLinux> logger) : IDesktopClientPermissionService
{
  private readonly ILogger<DesktopClientPermissionServiceLinux> _logger = logger;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public Task<DesktopClientPermissionState> GetPermissionState(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
    return GetPlatformPermissionState(cancellationToken);
  }

  public async Task<DesktopClientPermissionState> GetPlatformPermissionState(CancellationToken cancellationToken = default)
  {
    var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
    if (!detector.IsWayland())
    {
      return new DesktopClientPermissionState(true);
    }

    var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
    var isWaylandRemoteControlGranted = await waylandPermissions.IsRemoteControlPermissionGranted();

    _logger.LogInformation(
      "Wayland desktop client permission state: RemoteControl={RemoteControl}",
      isWaylandRemoteControlGranted);

    return new DesktopClientPermissionState(
      ArePermissionsGranted: isWaylandRemoteControlGranted,
      Reason: isWaylandRemoteControlGranted
        ? null
        : "Wayland remote control permission is not granted on the desktop client.",
      IsWaylandRemoteControlGranted: isWaylandRemoteControlGranted);
  }

  public async Task<DesktopClientPermissionState> RequestPermission(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
    var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
    if (detector.IsWayland())
    {
      var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
      var isGranted = await waylandPermissions.RequestRemoteControlPermission(bypassRestoreToken: true, cancellationToken: cancellationToken);

      _logger.LogInformation(
        "Wayland remote control permission request result: Granted={Granted}",
        isGranted);

      return new DesktopClientPermissionState(
        ArePermissionsGranted: isGranted,
        Reason: isGranted ? null : "Wayland remote control permission request was denied or failed.",
        IsWaylandRemoteControlGranted: isGranted);
    }

    return new DesktopClientPermissionState(true);
  }
}
