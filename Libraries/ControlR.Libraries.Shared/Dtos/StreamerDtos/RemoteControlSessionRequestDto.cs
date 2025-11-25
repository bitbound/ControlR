using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
[method: JsonConstructor]
[method: SerializationConstructor]
public record RemoteControlSessionRequestDto(
  Guid SessionId,
  Uri WebsocketUri,
  int TargetSystemSession,
  int TargetProcessId,
  string ViewerConnectionId,
  Guid DeviceId,
  bool NotifyUserOnSessionStart,
  bool RequireConsent,
  string ViewerName = "");