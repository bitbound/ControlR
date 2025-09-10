namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetRootDrivesRequestDto(
  Guid DeviceId);
