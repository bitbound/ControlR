using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record CreateDeviceRequestDto(
  DeviceUpdateRequestDto Device,
  Guid InstallerKeyId,
  string InstallerKeySecret,
  Guid[]? TagIds = null);
