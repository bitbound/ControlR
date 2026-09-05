namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SendWakeDeviceRequestDto(
  Guid DeviceId,
  IReadOnlyList<string> MacAddresses);
