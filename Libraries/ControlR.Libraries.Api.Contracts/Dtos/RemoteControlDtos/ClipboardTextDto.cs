namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ClipboardTextDto(
    string? Text,
    Guid SessionId);