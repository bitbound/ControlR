namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DesktopClientDownloadProgressDto(
    Guid RemoteControlSessionId,
    string ViewerConnectionId,
    double Progress,
    string Message);
