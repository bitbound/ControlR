namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

/// <summary>
/// DTO kinds the desktop client issues and the viewer consumes.
/// </summary>
public enum RemoteControlDesktopDtoType
{
  None = 0,
  Ack = 39,
  BlockInputResult = 45,
  CaptureMetricsChanged = 40,
  // Also a RemoteControlViewerDtoType at the same value.  Both directions carry clipboard text.
  ClipboardText = 21,
  CursorChanged = 34,
  DisplayData = 23,
  PrivacyScreenResult = 47,
  RemoteControlSessionError = 49,
  ScreenRegion = 32,
  ScreenRegions = 50,
  SessionDisconnectRequested = 48,
  ToastNotification = 44,
  VideoStreamPacket = 42,
  WindowsSessionEnding = 36,
  WindowsSessionSwitched = 37,
}

public static class RemoteControlDesktopDtoTypeExtensions
{
  /// <summary>
  /// Raw screen captures stay on the WebSocket relay. Everything else travels over the hubs.
  /// </summary>
  public static bool IsScreenFrame(this RemoteControlDesktopDtoType dtoType) =>
    dtoType is RemoteControlDesktopDtoType.VideoStreamPacket
      or RemoteControlDesktopDtoType.ScreenRegion
      or RemoteControlDesktopDtoType.ScreenRegions;
}
