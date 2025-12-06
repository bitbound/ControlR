namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record CreateDeviceRequestDto(
  DeviceDto Device,
  Guid InstallerKeyId,
  string InstallerKey);
