using ControlR.Shared.Dtos;
using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Models;

[MessagePackObject]
[method: SerializationConstructor]
[method: JsonConstructor]
public class StreamerHubSession(Guid sessionId, DisplayDto[] displays, string streamerConnectionId)
{
    [MsgPackKey]
    public string StreamerConnectionId { get; init; } = streamerConnectionId;

    [MsgPackKey]
    public DisplayDto[] Displays { get; init; } = displays;

    [MsgPackKey]
    public string? AgentConnectionId { get; set; }

    [MsgPackKey]
    public Guid SessionId { get; init; } = sessionId;

    [MsgPackKey]
    public string? ViewerConnectionId { get; set; }
}
