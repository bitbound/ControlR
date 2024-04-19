namespace ControlR.Viewer.Models.Messages;
internal record StreamerDownloadProgressMessage(
    Guid StreamingSessionId, 
    double DownloadProgress, 
    string Message);