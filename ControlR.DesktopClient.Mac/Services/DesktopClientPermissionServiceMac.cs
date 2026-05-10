using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

internal class DesktopClientPermissionServiceMac(
  IServiceProvider serviceProvider,
  ILogger<DesktopClientPermissionServiceMac> logger) : IDesktopClientPermissionService
{
  private readonly ILogger<DesktopClientPermissionServiceMac> _logger = logger;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public async Task<DesktopClientPermissionState> GetPermissionState(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
    var platformState = await GetPlatformPermissionState(cancellationToken);

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
  }

  public Task<DesktopClientPermissionState> GetPlatformPermissionState(CancellationToken cancellationToken = default)
  {
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    var isAccessibilityGranted = macInterop.IsMacAccessibilityPermissionGranted();
    var isScreenCaptureGranted = macInterop.IsMacScreenCapturePermissionGranted();

    _logger.LogInformation(
      "macOS desktop client permission state: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
      isAccessibilityGranted,
      isScreenCaptureGranted);

    return Task.FromResult(new DesktopClientPermissionState(
      ArePermissionsGranted: isAccessibilityGranted && isScreenCaptureGranted,
      Reason: isScreenCaptureGranted
        ? isAccessibilityGranted
          ? null
          : "Accessibility permission is not granted on this Mac desktop client."
        : "Screen capture permission is not granted on this Mac desktop client.",
      IsAccessibilityGranted: isAccessibilityGranted,
      IsScreenCaptureGranted: isScreenCaptureGranted));
  }

  public Task<DesktopClientPermissionState> RequestPermission(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    var isScreenCaptureGranted = macInterop.RequestScreenCapturePermission();
    var isAccessibilityGranted = macInterop.RequestAccessibilityPermission();

    _logger.LogInformation(
      "macOS permission request result: ScreenCapture={ScreenCapture}, Accessibility={Accessibility}",
      isScreenCaptureGranted,
      isAccessibilityGranted);

    return Task.FromResult(new DesktopClientPermissionState(
      ArePermissionsGranted: isScreenCaptureGranted && isAccessibilityGranted,
      Reason: isScreenCaptureGranted
        ? isAccessibilityGranted
          ? null
          : "Accessibility permission is not granted."
        : "Screen capture permission is not granted.",
      IsAccessibilityGranted: isAccessibilityGranted,
      IsScreenCaptureGranted: isScreenCaptureGranted));
  }
}
