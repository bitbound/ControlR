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
  Guid deviceId,
  bool notifyUserOnSessionStart,
  string viewerName = "")
{
  [MsgPackKey]
  public Guid DeviceId { get; init; } = deviceId;

  [MsgPackKey]
  public bool NotifyUserOnSessionStart { get; init; } = notifyUserOnSessionStart;

  [MsgPackKey]
  public Guid SessionId { get; init; } = sessionId;


  [MsgPackKey]
  public int TargetSystemSession { get; init; } = targetSystemSession;

  [MsgPackKey]
  public string ViewerConnectionId { get; init; } = viewerConnectionId;

  [MsgPackKey]
  public string ViewerName { get; set; } = viewerName;

  [MsgPackKey]
  public Uri WebsocketUri { get; init; } = websocketUri;
}