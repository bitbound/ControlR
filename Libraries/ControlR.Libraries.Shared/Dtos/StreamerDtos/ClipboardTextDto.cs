﻿namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ClipboardTextDto(
    string? Text,
    Guid SessionId);