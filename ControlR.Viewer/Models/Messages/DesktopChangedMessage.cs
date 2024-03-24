namespace ControlR.Viewer.Models.Messages;
internal class DesktopChangedMessage(Guid sessionId, string desktopName)
{
    public Guid SessionId { get; } = sessionId;
    public string DesktopName { get; } = desktopName;
}
