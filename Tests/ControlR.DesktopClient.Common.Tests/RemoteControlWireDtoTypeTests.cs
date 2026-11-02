using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Common.Tests;

public class RemoteControlWireDtoTypeTests
{
  [Fact]
  public void DesktopOnlyTagsMapToNoneForTheCommandHandler()
  {
    Assert.Equal(RemoteControlViewerDtoType.None, RemoteControlWireDtoType.VideoStreamPacket.ToViewerDtoType());
    Assert.Equal(RemoteControlViewerDtoType.None, RemoteControlWireDtoType.ToastNotification.ToViewerDtoType());
  }

  [Fact]
  public void EveryViewerKindRoundTripsThroughWire()
  {
    foreach (var viewerKind in Enum.GetValues<RemoteControlViewerDtoType>())
    {
      Assert.Equal(viewerKind, viewerKind.ToWire().ToViewerDtoType());
    }
  }

  [Fact]
  public void SharedClipboardTagRoundTripsBothDirections()
  {
    Assert.Equal(
      RemoteControlViewerDtoType.ClipboardText,
      RemoteControlDesktopDtoType.ClipboardText.ToWire().ToViewerDtoType());
    Assert.Equal(
      RemoteControlDesktopDtoType.ClipboardText,
      RemoteControlViewerDtoType.ClipboardText.ToWire().ToDesktopDtoType());
  }

  [Fact]
  public void WireTagValuesMatchTheReleasedDtoType()
  {
    var expected = new (RemoteControlWireDtoType Tag, int Value)[]
    {
      (RemoteControlWireDtoType.None, 0),
      (RemoteControlWireDtoType.ChangeDisplays, 17),
      (RemoteControlWireDtoType.CloseRemoteControlSession, 19),
      (RemoteControlWireDtoType.ClipboardText, 21),
      (RemoteControlWireDtoType.DisplayData, 23),
      (RemoteControlWireDtoType.MovePointer, 26),
      (RemoteControlWireDtoType.MouseButtonEvent, 27),
      (RemoteControlWireDtoType.KeyEvent, 28),
      (RemoteControlWireDtoType.TypeText, 29),
      (RemoteControlWireDtoType.ResetKeyboardState, 30),
      (RemoteControlWireDtoType.WheelScroll, 31),
      (RemoteControlWireDtoType.ScreenRegion, 32),
      (RemoteControlWireDtoType.MouseClick, 33),
      (RemoteControlWireDtoType.CursorChanged, 34),
      (RemoteControlWireDtoType.RequestClipboardText, 35),
      (RemoteControlWireDtoType.WindowsSessionEnding, 36),
      (RemoteControlWireDtoType.WindowsSessionSwitched, 37),
      (RemoteControlWireDtoType.Ack, 39),
      (RemoteControlWireDtoType.CaptureMetricsChanged, 40),
      (RemoteControlWireDtoType.RequestKeyFrame, 41),
      (RemoteControlWireDtoType.VideoStreamPacket, 42),
      (RemoteControlWireDtoType.ToggleBlockInput, 43),
      (RemoteControlWireDtoType.ToastNotification, 44),
      (RemoteControlWireDtoType.BlockInputResult, 45),
      (RemoteControlWireDtoType.TogglePrivacyScreen, 46),
      (RemoteControlWireDtoType.PrivacyScreenResult, 47),
      (RemoteControlWireDtoType.SessionDisconnectRequested, 48),
      (RemoteControlWireDtoType.RemoteControlSessionError, 49),
      (RemoteControlWireDtoType.ScreenRegions, 50),
      (RemoteControlWireDtoType.UpdateCaptureSettings, 51)
    };

    var tags = Enum.GetValues<RemoteControlWireDtoType>();
    Assert.Equal(expected.Length, tags.Length);

    foreach (var (tag, value) in expected)
    {
      Assert.Equal(value, (int)tag);
    }
  }
}
