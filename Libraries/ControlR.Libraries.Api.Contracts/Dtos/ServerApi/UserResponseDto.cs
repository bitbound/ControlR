namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record UserResponseDto(
  Guid Id,
  string? UserName,
  string? Email);