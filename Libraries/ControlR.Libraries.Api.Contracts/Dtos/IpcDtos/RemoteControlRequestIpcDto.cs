using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record RemoteControlRequestIpcDto(
  Guid SessionId,
  Uri WebsocketUri,
  int TargetSystemSession,
  int TargetProcessId,
  Guid DeviceId,
  bool NotifyUserOnSessionStart,
  bool RequireConsent,
  string? ViewerConnectionId,
  string? ViewerName)
{
  /// <summary>
  /// The sealed command set the session host must enforce on the relay command lane. Null when the
  /// agent predates command policies.
  /// </summary>
  public RemoteControlCommandPolicyDto? CommandPolicy { get; init; }
}