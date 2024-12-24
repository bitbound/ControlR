namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject]
public record CreateDeviceRequestDto(
  [property: Key(0)] DeviceDto Device,
  [property: Key(1)] string InstallationKey);
