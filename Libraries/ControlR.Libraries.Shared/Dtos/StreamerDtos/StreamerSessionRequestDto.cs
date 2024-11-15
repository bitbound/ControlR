using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public record StreamerSessionRequestDto(
  [property: MsgPackKey] Guid SessionId,
  [property: MsgPackKey] Uri WebsocketUri,
  [property: MsgPackKey] int TargetSystemSession,
  [property: MsgPackKey] string ViewerConnectionId,
  [property: MsgPackKey] Guid DeviceId,
  [property: MsgPackKey] bool NotifyUserOnSessionStart,
  [property: MsgPackKey] string ViewerName = "");