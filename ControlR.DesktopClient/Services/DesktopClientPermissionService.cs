using System.Diagnostics;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class DesktopClientPermissionService(
  IServiceProvider serviceProvider,
  ILogger<DesktopClientPermissionService> logger) : IDesktopClientPermissionService
{
  private readonly ILogger<DesktopClientPermissionService> _logger = logger;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public async Task<DesktopClientPermissionState> GetPermissionState(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
    // Prevent fields from being refactored out due to not being used on some platforms.
    Debug.Assert(_logger != null, "Logger should not be null.");
    Debug.Assert(_serviceProvider != null, "Service provider should not be null.");

    var platformState = await GetPlatformPermissionState(cancellationToken);

#if IS_MACOS
      if (scope == DesktopClientPermissionScope.DesktopPreview)
      {
        if (platformState.IsScreenCaptureGranted == false)
        {
          return platformState with
          {
            ArePermissionsGranted = false,
            Reason = "Screen capture permission is not granted on this Mac desktop client."
          };
        }

        return platformState with
        {
          ArePermissionsGranted = true,
          Reason = null
        };
      }

      if (platformState.IsScreenCaptureGranted == false)
      {
        return platformState with
        {
          ArePermissionsGranted = false,
          Reason = "Screen capture permission is not granted on this Mac desktop client."
        };
      }

      if (platformState.IsAccessibilityGranted == false)
      {
        return platformState with
        {
          ArePermissionsGranted = false,
          Reason = "Accessibility permission is not granted on this Mac desktop client."
        };
      }

      return platformState with
      {
        ArePermissionsGranted = true,
        Reason = null
      };
#else
    return platformState;
#endif
  }

  public async Task<DesktopClientPermissionState> GetPlatformPermissionState(CancellationToken cancellationToken = default)
  {
#if IS_MACOS
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      var isAccessibilityGranted = macInterop.IsMacAccessibilityPermissionGranted();
      var isScreenCaptureGranted = macInterop.IsMacScreenCapturePermissionGranted();

      _logger.LogInformation(
        "macOS desktop client permission state: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
        isAccessibilityGranted,
        isScreenCaptureGranted);

      return new DesktopClientPermissionState(
        ArePermissionsGranted: isAccessibilityGranted && isScreenCaptureGranted,
        Reason: isScreenCaptureGranted
          ? isAccessibilityGranted
            ? null
            : "Accessibility permission is not granted on this Mac desktop client."
          : "Screen capture permission is not granted on this Mac desktop client.",
        IsAccessibilityGranted: isAccessibilityGranted,
        IsScreenCaptureGranted: isScreenCaptureGranted);
#elif IS_LINUX
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
#else
      return new DesktopClientPermissionState(true);
#endif
  }

  public async Task<DesktopClientPermissionState> RequestPermission(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
#if IS_LINUX
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
#elif IS_MACOS
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      var isScreenCaptureGranted = macInterop.RequestScreenCapturePermission();
      var isAccessibilityGranted = macInterop.RequestAccessibilityPermission();

      _logger.LogInformation(
        "macOS permission request result: ScreenCapture={ScreenCapture}, Accessibility={Accessibility}",
        isScreenCaptureGranted,
        isAccessibilityGranted);

      return new DesktopClientPermissionState(
        ArePermissionsGranted: isScreenCaptureGranted && isAccessibilityGranted,
        Reason: isScreenCaptureGranted
          ? isAccessibilityGranted
            ? null
            : "Accessibility permission is not granted."
          : "Screen capture permission is not granted.",
        IsAccessibilityGranted: isAccessibilityGranted,
        IsScreenCaptureGranted: isScreenCaptureGranted);
#else
      return new DesktopClientPermissionState(true);
#endif
  }
}