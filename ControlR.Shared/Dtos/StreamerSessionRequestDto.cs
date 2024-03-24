using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class StreamerSessionRequestDto(Guid streamingSessionId, int targetSystemSession, string? targetDesktop, string? viewerConnectionId)
{
    [MsgPackKey]
    public Guid StreamingSessionId { get; init; } = streamingSessionId;

    [MsgPackKey]
    public int TargetSystemSession { get; init; } = targetSystemSession;

    [MsgPackKey]
    public string? ViewerConnectionId { get; init; } = viewerConnectionId;

    [MsgPackKey]
    public string? TargetDesktop { get; init; } = targetDesktop;
}
