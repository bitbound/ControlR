using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CursorChangedDto(
    PointerCursor Cursor,
    string? CustomCursorBase64Png,
    ushort XHotspot,
    ushort YHotspot,
    Guid SessionId);