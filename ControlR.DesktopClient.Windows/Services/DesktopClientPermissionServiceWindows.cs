using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

namespace ControlR.DesktopClient.Windows.Services;

internal class DesktopClientPermissionServiceWindows : IDesktopClientPermissionService
{
  public Task<DesktopClientPermissionState> GetPermissionState(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
    return GetPlatformPermissionState(cancellationToken);
  }

  public Task<DesktopClientPermissionState> GetPlatformPermissionState(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(new DesktopClientPermissionState(true));
  }

  public Task<DesktopClientPermissionState> RequestPermission(
    DesktopClientPermissionScope scope,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(new DesktopClientPermissionState(true));
  }
}
