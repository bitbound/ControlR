namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record ClipboardChangeDto(
    [property: MsgPackKey] string? Text);