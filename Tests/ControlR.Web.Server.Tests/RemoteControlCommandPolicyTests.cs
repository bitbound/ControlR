using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Web.Server.Tests;

public class RemoteControlCommandPolicyTests
{
  [Fact]
  public void Create_SortsDedupesAndSealsTheAllowedSet()
  {
    var sessionId = Guid.NewGuid();

    var policy = RemoteControlCommandPolicyDto.Create(
      sessionId,
      [
        RemoteControlViewerDtoType.TypeText,
        RemoteControlViewerDtoType.KeyEvent,
        RemoteControlViewerDtoType.TypeText,
      ]);

    Assert.Equal(sessionId, policy.SessionId);
    Assert.Equal(RemoteControlCommandPolicyDto.CurrentPolicyVersion, policy.PolicyVersion);
    Assert.Equal(
      [RemoteControlViewerDtoType.KeyEvent, RemoteControlViewerDtoType.TypeText],
      policy.AllowedDtoTypes);
    Assert.False(string.IsNullOrEmpty(policy.PolicyHash));
    Assert.True(policy.IsBoundTo(sessionId));

    var sameAgain = RemoteControlCommandPolicyDto.Create(
      sessionId,
      [RemoteControlViewerDtoType.TypeText, RemoteControlViewerDtoType.KeyEvent]);
    Assert.Equal(policy.PolicyHash, sameAgain.PolicyHash);
  }

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

  [Fact]
  public void IsBoundTo_FalseWhenAllowedSetIsTampered()
  {
    var sessionId = Guid.NewGuid();
    var policy = RemoteControlCommandPolicyDto.Create(
      sessionId,
      [RemoteControlViewerDtoType.KeyEvent]);

    var smuggled = policy with
    {
      AllowedDtoTypes =
      [
        .. policy.AllowedDtoTypes,
        RemoteControlViewerDtoType.TogglePrivacyScreen,
      ],
    };

    Assert.False(smuggled.IsBoundTo(sessionId));

    var badHash = policy with { PolicyHash = "deadbeefdeadbeef" };
    Assert.False(badHash.IsBoundTo(sessionId));

    var emptyHash = policy with { PolicyHash = string.Empty };
    Assert.False(emptyHash.IsBoundTo(sessionId));
  }

  [Fact]
  public void IsBoundTo_FalseWhenPolicyVersionIsTampered()
  {
    var sessionId = Guid.NewGuid();
    var policy = RemoteControlCommandPolicyDto.Create(
      sessionId,
      [RemoteControlViewerDtoType.KeyEvent]);

    var bumped = policy with { PolicyVersion = RemoteControlCommandPolicyDto.CurrentPolicyVersion + 1 };
    Assert.False(bumped.IsBoundTo(sessionId));
  }

  [Fact]
  public void IsBoundTo_FalseWhenSessionIdDiffers()
  {
    var policy = RemoteControlCommandPolicyDto.Create(
      Guid.NewGuid(),
      [RemoteControlViewerDtoType.KeyEvent]);

    Assert.False(policy.IsBoundTo(Guid.NewGuid()));
  }
}
