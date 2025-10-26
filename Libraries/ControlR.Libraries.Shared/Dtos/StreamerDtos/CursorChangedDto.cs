using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CursorChangedDto(
    PointerCursor Cursor,
    string? CustomCursorBase64Png,
    ushort XHotspot,
    ushort YHotspot,
    Guid SessionId);