using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record CursorChangedDto(
    [property: MsgPackKey] WindowsCursor Cursor,
    [property: MsgPackKey] Guid SessionId);