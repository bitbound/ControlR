namespace ControlR.Agent.Models;

internal class VncSession(Guid sessionId)
{
    public Guid SessionId { get; } = sessionId;
}