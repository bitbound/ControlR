namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ClipboardTextDto(
    [property: MsgPackKey] string? Text,
    [property: MsgPackKey] Guid SessionId);