using ControlR.Shared.Dtos;
using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Models;

[MessagePackObject]
[method: SerializationConstructor]
[method: JsonConstructor]
public class StreamerHubSession
{
    public StreamerHubSession(Guid sessionId, DisplayDto[] displays, string streamerConnectionId)
    {
        SessionId = sessionId;
        Displays = displays;
        StreamerConnectionId = streamerConnectionId;
    }

    public StreamerHubSession(Guid sessionId, string agentConnectionId, string viewerConnectionId)
    {
        SessionId = sessionId;
        AgentConnectionId = agentConnectionId;
        ViewerConnectionId = viewerConnectionId;
    }

    [MsgPackKey]
    public string? AgentConnectionId { get; set; }

    [MsgPackKey]
    public DisplayDto[] Displays { get; set; } = [];

    [MsgPackKey]
    public Guid SessionId { get; set; }

    [MsgPackKey]
    public string? StreamerConnectionId { get; set; }
    [MsgPackKey]
    public string? ViewerConnectionId { get; set; }
}
