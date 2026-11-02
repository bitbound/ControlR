using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Authz;

/// <summary>
/// Map from a viewer command to the device permission it requires. No product code reads this
/// yet. The UI checks the same permissions per control (#423), and the planned enforcement (#424)
/// will read this map so the two cannot drift from the grant model.
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
