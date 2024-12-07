namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ClipboardTextDto(
    [property: Key(0)] string? Text,
    [property: Key(1)] Guid SessionId);