namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record UserTagAddRequestDto(
  Guid UserId,
  Guid TagId);