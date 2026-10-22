namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

/// <summary>
/// DTO kinds the viewer issues and the desktop client consumes.
/// </summary>
public enum RemoteControlCommandDtoType
{
  None = 0,
  ChangeDisplays = 17,
  // Also a RemoteControlEventDtoType at the same value.  Both directions carry clipboard text.
  ClipboardText = 21,
  CloseRemoteControlSession = 19,
  KeyEvent = 28,
  MouseButtonEvent = 27,
  MouseClick = 33,
  MovePointer = 26,
  RequestClipboardText = 35,
  RequestKeyFrame = 41,
  ResetKeyboardState = 30,
  ToggleBlockInput = 43,
  TogglePrivacyScreen = 46,
  TypeText = 29,
  UpdateCaptureSettings = 51,
  WheelScroll = 31,
}
