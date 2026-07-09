namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.ServiceAccounts;

public record ServiceAccountCredentialDto(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? RevokedAt,
  DateTimeOffset? LastUsedAt);
