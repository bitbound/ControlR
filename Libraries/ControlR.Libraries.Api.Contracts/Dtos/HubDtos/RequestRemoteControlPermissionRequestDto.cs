namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record RequestRemoteControlPermissionRequestDto(
  Guid DeviceId,
  int TargetProcessId);
