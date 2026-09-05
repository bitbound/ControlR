namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SendPowerStateChangeRequestDto(
  Guid DeviceId,
  PowerStateChangeType ChangeType);
