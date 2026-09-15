namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

public record PersonalAccessTokenResponseDto(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? LastUsed,
  int PermissionCount,
  PersonalAccessTokenPermissionMode PermissionMode);
