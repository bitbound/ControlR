using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
[method: JsonConstructor]
[method: SerializationConstructor]
public record RemoteControlSessionRequestDto(
  Guid SessionId,
  Uri WebsocketUri,
  int TargetSystemSession,
  int TargetProcessId,
  Guid DeviceId,
  bool NotifyUserOnSessionStart,
  bool RequireConsent)
{
  public string? ViewerConnectionId { get; init; }
  public string? ViewerName { get; init; }
}