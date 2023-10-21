using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class VncSessionRequest(Guid sessionId, string sessionPassword, string? viewerConnectionId)
{
    [MsgPackKey]
    public Guid SessionId { get; init; } = sessionId;

    [MsgPackKey]
    public string SessionPassword { get; init; } = sessionPassword;

    [MsgPackKey]
    public string? ViewerConnectionId { get; init; } = viewerConnectionId;
}