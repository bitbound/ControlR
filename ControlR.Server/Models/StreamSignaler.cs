using System.Net.WebSockets;

namespace ControlR.Server.Models;

public class StreamSignaler(Guid sessionId)
{
    public SemaphoreSlim AgentVncReady { get; } = new(0, 1);
    public WebSocket? AgentVncWebsocket { get; internal set; }
    public SemaphoreSlim NoVncViewerReady { get; } = new(0, 1);
    public WebSocket? NoVncWebsocket { get; internal set; }
    public Guid SessionId { get; init; } = sessionId;
}