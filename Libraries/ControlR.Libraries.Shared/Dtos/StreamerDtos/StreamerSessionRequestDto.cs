using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
[method: JsonConstructor]
[method: SerializationConstructor]
public record StreamerSessionRequestDto(
  Guid SessionId,
  Uri WebsocketUri,
  int TargetSystemSession,
  string ViewerConnectionId,
  Guid DeviceId,
  bool NotifyUserOnSessionStart,
  string ViewerName = "");