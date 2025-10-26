using ControlR.Libraries.Shared.Enums;

namespace ControlR.DesktopClient.Common.Messages;

public record CursorChangedMessage(
  PointerCursor Cursor,
  string? CustomCursorBase64Png = null,
  ushort XHotspot = 0,
  ushort YHotspot = 0);