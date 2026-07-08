namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record UserResponseDto(
  Guid Id,
  string? UserName,
  string? Email);