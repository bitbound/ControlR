﻿using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record DisplayDataDto(
    [property: MsgPackKey] Guid SessionId,
    [property: MsgPackKey] DisplayDto[] Displays);