namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;

public record DeviceTagAddRequestDto(
  Guid DeviceId,
  Guid TagId);
