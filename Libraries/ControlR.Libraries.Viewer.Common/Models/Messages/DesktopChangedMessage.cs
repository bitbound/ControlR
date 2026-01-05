namespace ControlR.Libraries.Viewer.Common.Models.Messages;
internal class DesktopChangedMessage(Guid sessionId)
{
  public Guid SessionId { get; } = sessionId;
}
