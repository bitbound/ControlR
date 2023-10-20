using ControlR.Viewer.Enums;

namespace ControlR.Viewer.Models.Messages;
internal class RemoteDisplayWindowStateMessage(Guid sessionId, WindowState state)
{
    public Guid SessionId { get; } = sessionId;
    public WindowState State { get; } = state;
}
