namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SendRemoteControlDtoToAgentRequestDto(
  Guid DeviceId,
  int TargetProcessId,
  Guid SessionId,
  RemoteControlCommandWrapper Wrapper);
