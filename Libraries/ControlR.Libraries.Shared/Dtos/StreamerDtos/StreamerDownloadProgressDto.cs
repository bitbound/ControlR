namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record StreamerDownloadProgressDto(
    Guid StreamingSessionId,
    string ViewerConnectionId,
    double Progress,
    string Message);
