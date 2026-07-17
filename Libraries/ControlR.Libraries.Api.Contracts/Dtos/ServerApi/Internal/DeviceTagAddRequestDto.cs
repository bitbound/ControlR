namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record DeviceTagAddRequestDto(
  Guid DeviceId,
  Guid TagId);