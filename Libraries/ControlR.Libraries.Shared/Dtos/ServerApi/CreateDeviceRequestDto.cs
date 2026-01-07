using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record CreateDeviceRequestDto(
  DeviceUpdateRequestDto Device,
  Guid InstallerKeyId,
  string InstallerKeySecret,
  Guid[]? TagIds = null);
