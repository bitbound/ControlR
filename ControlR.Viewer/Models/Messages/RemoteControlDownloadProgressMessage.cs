namespace ControlR.Viewer.Models.Messages;
internal class RemoteControlDownloadProgressMessage(Guid streamingSessionId, double downloadProgress)
{
    public Guid StreamingSessionId { get; } = streamingSessionId;
    public double DownloadProgress { get; } = downloadProgress;
}
