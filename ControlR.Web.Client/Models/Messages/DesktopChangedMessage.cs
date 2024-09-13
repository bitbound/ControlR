namespace ControlR.Web.Client.Models.Messages;
internal class DesktopChangedMessage(Guid sessionId)
{
    public Guid SessionId { get; } = sessionId;
}
