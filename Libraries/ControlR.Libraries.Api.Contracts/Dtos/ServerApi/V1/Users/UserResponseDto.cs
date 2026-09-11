namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

public record UserResponseDto(
  Guid Id,
  string? UserName,
  string? Email,
  DateTimeOffset CreatedAt,
  IReadOnlyList<string> Permissions,
  string? DisplayName = null);
