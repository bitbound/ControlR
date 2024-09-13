using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class StreamerSessionRequestDto(
    Guid sessionId,
    Uri websocketUri,
    int targetSystemSession,
    string viewerConnectionId,
    string agentConnectionId,
    bool notifyUserOnSessionStart)
{
    [MsgPackKey]
    public string AgentConnectionId { get; init; } = agentConnectionId;

    [MsgPackKey]
    public bool NotifyUserOnSessionStart { get; init; } = notifyUserOnSessionStart;


    [MsgPackKey]
    public Guid SessionId { get; init; } = sessionId;


    [MsgPackKey]
    public int TargetSystemSession { get; init; } = targetSystemSession;

    [MsgPackKey]
    public string ViewerConnectionId { get; init; } = viewerConnectionId;

    [MsgPackKey]
    public Uri WebsocketUri { get; init; } = websocketUri;
}
