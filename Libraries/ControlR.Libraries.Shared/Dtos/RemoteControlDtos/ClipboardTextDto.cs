namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ClipboardTextDto(
    string? Text,
    Guid SessionId);