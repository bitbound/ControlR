using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.DesktopClient.Common.Messages;

public record CursorChangedMessage(
  PointerCursor Cursor,
  string? CustomCursorBase64Png = null,
  ushort XHotspot = 0,
  ushort YHotspot = 0);