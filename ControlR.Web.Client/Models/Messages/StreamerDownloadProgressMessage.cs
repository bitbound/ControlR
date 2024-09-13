namespace ControlR.Web.Client.Models.Messages;
internal record StreamerDownloadProgressMessage(
    Guid StreamingSessionId,
    double DownloadProgress,
    string Message);