namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ClipboardChangeDto(
    [property: MsgPackKey] string? Text,
    [property: MsgPackKey] Guid SessionId) : DtoRecordBase;