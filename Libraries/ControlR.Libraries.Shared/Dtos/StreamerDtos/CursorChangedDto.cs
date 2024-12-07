using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record CursorChangedDto(
    [property: Key(0)] WindowsCursor Cursor,
    [property: Key(1)] Guid SessionId);