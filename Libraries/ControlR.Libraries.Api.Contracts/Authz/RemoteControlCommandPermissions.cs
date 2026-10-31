using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Authz;

/// <summary>
/// Single source of truth for which device permission a viewer command requires, so an
/// enforcement point reading this map cannot drift from the grant model.
/// </summary>
public static class RemoteControlCommandPermissions
{
  public static IReadOnlyList<RemoteControlViewerDtoType> GetAllowedDtoTypes(
    IReadOnlySet<string> allowedPermissions)
  {
    return Enum.GetValues<RemoteControlViewerDtoType>()
      .Where(x => x != RemoteControlViewerDtoType.None && allowedPermissions.Contains(GetRequiredPermission(x)))
      .OrderBy(x => (int)x)
      .ToArray();
  }

  public static string GetRequiredPermission(RemoteControlViewerDtoType dtoType) => dtoType switch
  {
    RemoteControlViewerDtoType.ClipboardText => PermissionNames.DeviceClipboardWrite,
    RemoteControlViewerDtoType.RequestClipboardText => PermissionNames.DeviceClipboardRead,
    RemoteControlViewerDtoType.ToggleBlockInput => PermissionNames.DeviceRemoteControlBlockInput,
    RemoteControlViewerDtoType.TogglePrivacyScreen => PermissionNames.DeviceRemoteControlPrivacyScreen,
    RemoteControlViewerDtoType.RequestKeyFrame
      or RemoteControlViewerDtoType.UpdateCaptureSettings
      or RemoteControlViewerDtoType.CloseRemoteControlSession => PermissionNames.DeviceRemoteControlConnect,
    _ => PermissionNames.DeviceRemoteControlInteract
  };
}
