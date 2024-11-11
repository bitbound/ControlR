namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record DeviceTagAddRequestDto(
  Guid DeviceId,
  Guid TagId);