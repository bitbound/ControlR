namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DesktopClientDownloadProgressDto(
    Guid StreamingSessionId,
    string ViewerConnectionId,
    double Progress,
    string Message);
