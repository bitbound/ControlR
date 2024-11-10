namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record UserTagAddRequestDto(
  Guid UserId,
  Guid TagId);