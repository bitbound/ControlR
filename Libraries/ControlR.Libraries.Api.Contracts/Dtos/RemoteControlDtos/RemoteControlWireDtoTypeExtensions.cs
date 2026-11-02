namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

/// <summary>
/// Bridges the viewer and desktop kind enums through <see cref="RemoteControlWireDtoType"/>.
/// Command tags cross directions by member name, so a rename in either enum breaks compilation
/// here instead of silently retagging on the wire.
/// </summary>
public static class RemoteControlWireDtoTypeExtensions
{
  /// <summary>
  /// Maps a viewer command kind to its wire tag. Throws for an unmapped kind so a new command
  /// cannot ship without one.
  /// </summary>
  public static RemoteControlWireDtoType ToWire(this RemoteControlViewerDtoType value) => value switch
  {
    RemoteControlViewerDtoType.None => RemoteControlWireDtoType.None,
    RemoteControlViewerDtoType.ChangeDisplays => RemoteControlWireDtoType.ChangeDisplays,
    RemoteControlViewerDtoType.ClipboardText => RemoteControlWireDtoType.ClipboardText,
    RemoteControlViewerDtoType.CloseRemoteControlSession => RemoteControlWireDtoType.CloseRemoteControlSession,
    RemoteControlViewerDtoType.KeyEvent => RemoteControlWireDtoType.KeyEvent,
    RemoteControlViewerDtoType.MouseButtonEvent => RemoteControlWireDtoType.MouseButtonEvent,
    RemoteControlViewerDtoType.MouseClick => RemoteControlWireDtoType.MouseClick,
    RemoteControlViewerDtoType.MovePointer => RemoteControlWireDtoType.MovePointer,
    RemoteControlViewerDtoType.RequestClipboardText => RemoteControlWireDtoType.RequestClipboardText,
    RemoteControlViewerDtoType.RequestKeyFrame => RemoteControlWireDtoType.RequestKeyFrame,
    RemoteControlViewerDtoType.ResetKeyboardState => RemoteControlWireDtoType.ResetKeyboardState,
    RemoteControlViewerDtoType.ToggleBlockInput => RemoteControlWireDtoType.ToggleBlockInput,
    RemoteControlViewerDtoType.TogglePrivacyScreen => RemoteControlWireDtoType.TogglePrivacyScreen,
    RemoteControlViewerDtoType.TypeText => RemoteControlWireDtoType.TypeText,
    RemoteControlViewerDtoType.UpdateCaptureSettings => RemoteControlWireDtoType.UpdateCaptureSettings,
    RemoteControlViewerDtoType.WheelScroll => RemoteControlWireDtoType.WheelScroll,
    _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Viewer DTO kind has no wire tag.")
  };

  /// <summary>
  /// Reads a wire tag from the desktop-typed relay field. Every desktop kind is already a wire tag.
  /// </summary>
  public static RemoteControlWireDtoType ToWire(this RemoteControlDesktopDtoType value) => (RemoteControlWireDtoType)(int)value;

  /// <summary>
  /// Maps a wire tag back to a viewer command kind. Tags outside the viewer half, which covers
  /// stale or misdirected frames, map to None so the command handler logs and drops them.
  /// </summary>
  public static RemoteControlViewerDtoType ToViewerDtoType(this RemoteControlWireDtoType value) => value switch
  {
    RemoteControlWireDtoType.None => RemoteControlViewerDtoType.None,
    RemoteControlWireDtoType.ChangeDisplays => RemoteControlViewerDtoType.ChangeDisplays,
    RemoteControlWireDtoType.ClipboardText => RemoteControlViewerDtoType.ClipboardText,
    RemoteControlWireDtoType.CloseRemoteControlSession => RemoteControlViewerDtoType.CloseRemoteControlSession,
    RemoteControlWireDtoType.KeyEvent => RemoteControlViewerDtoType.KeyEvent,
    RemoteControlWireDtoType.MouseButtonEvent => RemoteControlViewerDtoType.MouseButtonEvent,
    RemoteControlWireDtoType.MouseClick => RemoteControlViewerDtoType.MouseClick,
    RemoteControlWireDtoType.MovePointer => RemoteControlViewerDtoType.MovePointer,
    RemoteControlWireDtoType.RequestClipboardText => RemoteControlViewerDtoType.RequestClipboardText,
    RemoteControlWireDtoType.RequestKeyFrame => RemoteControlViewerDtoType.RequestKeyFrame,
    RemoteControlWireDtoType.ResetKeyboardState => RemoteControlViewerDtoType.ResetKeyboardState,
    RemoteControlWireDtoType.ToggleBlockInput => RemoteControlViewerDtoType.ToggleBlockInput,
    RemoteControlWireDtoType.TogglePrivacyScreen => RemoteControlViewerDtoType.TogglePrivacyScreen,
    RemoteControlWireDtoType.TypeText => RemoteControlViewerDtoType.TypeText,
    RemoteControlWireDtoType.UpdateCaptureSettings => RemoteControlViewerDtoType.UpdateCaptureSettings,
    RemoteControlWireDtoType.WheelScroll => RemoteControlViewerDtoType.WheelScroll,
    _ => RemoteControlViewerDtoType.None
  };

  /// <summary>
  /// Encodes a wire tag into the relay envelope's desktop-typed field. Viewer command tags have no
  /// desktop name by design, so the value crosses as a number here and nowhere else.
  /// </summary>
  public static RemoteControlDesktopDtoType ToDesktopDtoType(this RemoteControlWireDtoType value) => (RemoteControlDesktopDtoType)(int)value;
}
