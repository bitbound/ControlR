﻿namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record StreamerDownloadProgressDto(
    [property: MsgPackKey] Guid StreamingSessionId,
    [property: MsgPackKey] string ViewerConnectionId,
    [property: MsgPackKey] double Progress,
    [property: MsgPackKey] string Message);
