using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class VncSessionRequest(Guid sessionId, string vncPassword, string? viewerConnectionId)
{
    [MsgPackKey]
    public Guid SessionId { get; init; } = sessionId;

    [MsgPackKey]
    public string? ViewerConnectionId { get; init; } = viewerConnectionId;

    [MsgPackKey]
    public string VncPassword { get; init; } = vncPassword;
}