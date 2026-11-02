using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Web.Server.Tests;

public class RemoteControlCommandPermissionsTests
{
  [Fact]
  public void GetRequiredPermission_MapsSpecialCommandsToTheirDedicatedPermissions()
  {
    Assert.Equal(
      PermissionNames.DeviceClipboardWrite,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlDtoType.ClipboardText));
    Assert.Equal(
      PermissionNames.DeviceClipboardRead,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlDtoType.RequestClipboardText));
    Assert.Equal(
      PermissionNames.DeviceRemoteControlBlockInput,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlDtoType.ToggleBlockInput));
    Assert.Equal(
      PermissionNames.DeviceRemoteControlPrivacyScreen,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlDtoType.TogglePrivacyScreen));
    Assert.Equal(
      PermissionNames.DeviceRemoteControlInteract,
      RemoteControlCommandPermissions.GetRequiredPermission(RemoteControlDtoType.KeyEvent));
  }
}
