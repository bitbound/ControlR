namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record StartDeviceAccessActivityRequestDto(Guid DeviceId);
