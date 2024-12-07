using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public record StreamerSessionRequestDto(
  [property: Key(0)] Guid SessionId,
  [property: Key(1)] Uri WebsocketUri,
  [property: Key(2)] int TargetSystemSession,
  [property: Key(3)] string ViewerConnectionId,
  [property: Key(4)] Guid DeviceId,
  [property: Key(5)] bool NotifyUserOnSessionStart,
  [property: Key(6)] string ViewerName = "");