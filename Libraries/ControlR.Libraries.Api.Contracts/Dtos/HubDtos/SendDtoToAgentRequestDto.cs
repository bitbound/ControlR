namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SendDtoToAgentRequestDto(
  Guid DeviceId,
  DtoWrapper Wrapper);
