namespace ControlR.Viewer.Models.Messages;
internal class DesktopChangedMessage(Guid sessionId)
{
    public Guid SessionId { get; } = sessionId;
}
