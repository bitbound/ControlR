using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
[method: JsonConstructor]
[method: SerializationConstructor]
public record VncSessionRequestDto(
  Guid SessionId,
  Uri WebsocketUri,
  string ViewerConnectionId,
  Guid DeviceId,
  bool NotifyUserOnSessionStart,
  int Port)
{
  public string? ViewerName { get; set; }
}