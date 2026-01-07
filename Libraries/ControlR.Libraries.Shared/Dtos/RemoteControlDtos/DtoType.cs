namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

public enum DtoType
{
  None = 0,
  ChangeDisplays = 17,
  CloseRemoteControlSession = 19,
  ClipboardText = 21,
  DisplayData = 23,
  MovePointer = 26,
  MouseButtonEvent = 27,
  KeyEvent = 28,
  TypeText = 29,
  ResetKeyboardState = 30,
  WheelScroll = 31,
  ScreenRegion = 32,
  MouseClick = 33,
  CursorChanged = 34,
  RequestClipboardText = 35,
  WindowsSessionEnding = 36,
  WindowsSessionSwitched = 37,
  Ack = 39,
  CaptureMetricsChanged = 40,
  RequestKeyFrame = 41,
  VideoStreamPacket = 42,
  ToggleBlockInput = 43,
  ToastNotification = 44,
  BlockInputResult = 45,
}