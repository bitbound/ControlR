﻿using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CursorChangedDto(
    WindowsCursor Cursor,
    Guid SessionId);