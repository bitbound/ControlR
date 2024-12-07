namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record StreamerDownloadProgressDto(
    [property: Key(0)] Guid StreamingSessionId,
    [property: Key(1)] string ViewerConnectionId,
    [property: Key(2)] double Progress,
    [property: Key(3)] string Message);
