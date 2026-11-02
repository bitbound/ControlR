using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Common.Tests;

public class RemoteControlDtoTypeTests
{
  [Fact]
  public void ValuesMatchTheReleasedTagSpace()
  {
    var expected = new (RemoteControlDtoType Tag, int Value)[]
    {
      (RemoteControlDtoType.None, 0),
      (RemoteControlDtoType.ChangeDisplays, 17),
      (RemoteControlDtoType.CloseRemoteControlSession, 19),
      (RemoteControlDtoType.ClipboardText, 21),
      (RemoteControlDtoType.DisplayData, 23),
      (RemoteControlDtoType.MovePointer, 26),
      (RemoteControlDtoType.MouseButtonEvent, 27),
      (RemoteControlDtoType.KeyEvent, 28),
      (RemoteControlDtoType.TypeText, 29),
      (RemoteControlDtoType.ResetKeyboardState, 30),
      (RemoteControlDtoType.WheelScroll, 31),
      (RemoteControlDtoType.ScreenRegion, 32),
      (RemoteControlDtoType.MouseClick, 33),
      (RemoteControlDtoType.CursorChanged, 34),
      (RemoteControlDtoType.RequestClipboardText, 35),
      (RemoteControlDtoType.WindowsSessionEnding, 36),
      (RemoteControlDtoType.WindowsSessionSwitched, 37),
      (RemoteControlDtoType.Ack, 39),
      (RemoteControlDtoType.CaptureMetricsChanged, 40),
      (RemoteControlDtoType.RequestKeyFrame, 41),
      (RemoteControlDtoType.VideoStreamPacket, 42),
      (RemoteControlDtoType.ToggleBlockInput, 43),
      (RemoteControlDtoType.ToastNotification, 44),
      (RemoteControlDtoType.BlockInputResult, 45),
      (RemoteControlDtoType.TogglePrivacyScreen, 46),
      (RemoteControlDtoType.PrivacyScreenResult, 47),
      (RemoteControlDtoType.SessionDisconnectRequested, 48),
      (RemoteControlDtoType.RemoteControlSessionError, 49),
      (RemoteControlDtoType.ScreenRegions, 50),
      (RemoteControlDtoType.UpdateCaptureSettings, 51)
    };

    var tags = Enum.GetValues<RemoteControlDtoType>();
    Assert.Equal(expected.Length, tags.Length);

    foreach (var (tag, value) in expected)
    {
      Assert.Equal(value, (int)tag);
    }
  }
}
