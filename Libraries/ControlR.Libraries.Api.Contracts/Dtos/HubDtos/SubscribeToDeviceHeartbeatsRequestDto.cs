namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SubscribeToDeviceHeartbeatsRequestDto(
  IReadOnlyList<Guid> DeviceIds);
