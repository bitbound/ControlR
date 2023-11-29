namespace ControlR.Agent.Models;

internal class VncSession(Guid sessionId, bool autoRunUsed)
{
    public bool AutoRunUsed { get; } = autoRunUsed;
    public Guid SessionId { get; } = sessionId;
}