using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public sealed record DesktopClientPermissionState(
  bool ArePermissionsGranted,
  string? Reason = null,
  bool? IsAccessibilityGranted = null,
  bool? IsScreenCaptureGranted = null,
  bool? IsWaylandRemoteControlGranted = null);

public interface IDesktopClientPermissionService
{
  Task<DesktopClientPermissionState> GetPermissionState(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default);

  Task<DesktopClientPermissionState> GetPlatformPermissionState(CancellationToken cancellationToken = default);

  Task<DesktopClientPermissionState> RequestPermission(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default);
}