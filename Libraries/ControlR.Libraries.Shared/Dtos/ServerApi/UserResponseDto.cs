namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record UserResponseDto(
  Guid Id,
  string? UserName,
  string? Email) : IHasPrimaryKey;