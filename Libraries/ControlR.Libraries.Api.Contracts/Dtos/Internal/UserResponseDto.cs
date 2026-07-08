namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record UserResponseDto(
  Guid Id,
  string? UserName,
  string? Email);