using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Authz;

/// <summary>
/// Map from a viewer command to the device permission it requires. No product code reads this
/// yet. The UI checks the same permissions per control (#423), and the planned enforcement (#424)
/// will read this map so the two cannot drift from the grant model.
/// </summary>
public static class RemoteControlCommandPermissions
{
  public static string GetRequiredPermission(RemoteControlDtoType dtoType) => dtoType switch
  {
    RemoteControlDtoType.ClipboardText => PermissionNames.DeviceClipboardWrite,
    RemoteControlDtoType.RequestClipboardText => PermissionNames.DeviceClipboardRead,
    RemoteControlDtoType.ToggleBlockInput => PermissionNames.DeviceRemoteControlBlockInput,
    RemoteControlDtoType.TogglePrivacyScreen => PermissionNames.DeviceRemoteControlPrivacyScreen,
    RemoteControlDtoType.RequestKeyFrame
      or RemoteControlDtoType.UpdateCaptureSettings
      or RemoteControlDtoType.CloseRemoteControlSession => PermissionNames.DeviceRemoteControlConnect,
    _ => PermissionNames.DeviceRemoteControlInteract
  };
}
