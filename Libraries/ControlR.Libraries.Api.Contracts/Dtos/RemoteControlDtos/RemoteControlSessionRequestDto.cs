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
  /// <summary>
  /// Set by the server only. The sealed command set the responder must enforce for this session.
  /// Null when the server predates command policies.
  /// </summary>
  public RemoteControlCommandPolicyDto? CommandPolicy { get; init; }

  public string? ViewerConnectionId { get; init; }
  public string? ViewerName { get; init; }

  /// <summary>
  /// Set by the server from the hub method the viewer called, never by the viewer itself.
  /// A viewer older than v0.30.x receives remote control DTOs over the WebSocket relay.
  /// </summary>
  public bool ViewerSupportsRemoteControlDtoHub { get; init; }
}