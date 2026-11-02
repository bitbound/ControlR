using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Web.Server.Tests;

public class RemoteControlCommandPermissionsTests
{
  [Fact]
  public void GetAllowedDtoTypes_ConnectOnly_CoversOnlyConnectionMaintenanceCommands()
  {
    var allowed = RemoteControlCommandPermissions.GetAllowedDtoTypes(
      new HashSet<string> { PermissionNames.DeviceRemoteControlConnect });

    Assert.Equal(
      [
        RemoteControlViewerDtoType.CloseRemoteControlSession,
        RemoteControlViewerDtoType.RequestKeyFrame,
        RemoteControlViewerDtoType.UpdateCaptureSettings,
      ],
      allowed);
  }

  [Fact]
  public void GetAllowedDtoTypes_FullPermissionSet_CoversEveryCommandKind()
  {
    var allowed = RemoteControlCommandPermissions.GetAllowedDtoTypes(
      new HashSet<string>
      {
        PermissionNames.DeviceRemoteControlConnect,
        PermissionNames.DeviceRemoteControlInteract,
        PermissionNames.DeviceRemoteControlBlockInput,
        PermissionNames.DeviceRemoteControlPrivacyScreen,
        PermissionNames.DeviceClipboardRead,
        PermissionNames.DeviceClipboardWrite,
      });

    var expected = Enum.GetValues<RemoteControlViewerDtoType>()
      .Where(x => x != RemoteControlViewerDtoType.None)
      .ToArray();

    Assert.Equal(expected, allowed);
  }

  [Fact]
  public void GetAllowedDtoTypes_InteractOnly_ExcludesClipboardTogglesAndConnectTrio()
  {
    var allowed = RemoteControlCommandPermissions.GetAllowedDtoTypes(
      new HashSet<string> { PermissionNames.DeviceRemoteControlInteract });

    Assert.Contains(RemoteControlViewerDtoType.KeyEvent, allowed);
    Assert.Contains(RemoteControlViewerDtoType.MouseButtonEvent, allowed);
    Assert.Contains(RemoteControlViewerDtoType.TypeText, allowed);
    Assert.DoesNotContain(RemoteControlViewerDtoType.ClipboardText, allowed);
    Assert.DoesNotContain(RemoteControlViewerDtoType.RequestClipboardText, allowed);
    Assert.DoesNotContain(RemoteControlViewerDtoType.ToggleBlockInput, allowed);
    Assert.DoesNotContain(RemoteControlViewerDtoType.TogglePrivacyScreen, allowed);
    Assert.DoesNotContain(RemoteControlViewerDtoType.RequestKeyFrame, allowed);
    Assert.DoesNotContain(RemoteControlViewerDtoType.UpdateCaptureSettings, allowed);
    Assert.DoesNotContain(RemoteControlViewerDtoType.CloseRemoteControlSession, allowed);
  }

  [Fact]
  public void GetRequiredPermission_MapsSpecialCommandsToTheirDedicatedPermissions()
  {
    Assert.Equal(
      PermissionNames.DeviceClipboardWrite,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlViewerDtoType.ClipboardText));
    Assert.Equal(
      PermissionNames.DeviceClipboardRead,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlViewerDtoType.RequestClipboardText));
    Assert.Equal(
      PermissionNames.DeviceRemoteControlBlockInput,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlViewerDtoType.ToggleBlockInput));
    Assert.Equal(
      PermissionNames.DeviceRemoteControlPrivacyScreen,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlViewerDtoType.TogglePrivacyScreen));
    Assert.Equal(
      PermissionNames.DeviceRemoteControlInteract,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlViewerDtoType.KeyEvent));
  }
}
