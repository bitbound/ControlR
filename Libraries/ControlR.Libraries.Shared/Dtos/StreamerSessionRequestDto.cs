using ControlR.Libraries.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class StreamerSessionRequestDto(
    Guid sessionId,
    int targetSystemSession,
    string viewerConnectionId,
    string agentConnectionId,
    bool notifyUserOnSessionStart,
    bool lowerUacDuringSession,
    string? viewerName)
{
    [MsgPackKey]
    public string AgentConnectionId { get; init; } = agentConnectionId;

    [MsgPackKey]
    public bool NotifyUserOnSessionStart { get; init; } = notifyUserOnSessionStart;

    [MsgPackKey]
    public bool LowerUacDuringSession { get; init; } = lowerUacDuringSession;

    [MsgPackKey]
    public Guid SessionId { get; init; } = sessionId;


    [MsgPackKey]
    public int TargetSystemSession { get; init; } = targetSystemSession;

    [MsgPackKey]
    public string ViewerConnectionId { get; init; } = viewerConnectionId;

    [MsgPackKey]
    public string? ViewerName { get; init; } = viewerName;
}
