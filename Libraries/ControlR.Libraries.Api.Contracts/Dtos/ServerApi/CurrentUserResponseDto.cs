namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record CurrentUserResponseDto(
  Guid Id,
  string UserName,
  string Email,
  DateTimeOffset CreatedAt,
  bool IsOnline,
  bool RequirePasswordChange,
  bool TwoFactorEnabled,
  bool EmailConfirmed,
  Guid TenantId);