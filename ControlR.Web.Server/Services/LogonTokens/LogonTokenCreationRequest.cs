namespace ControlR.Web.Server.Services.LogonTokens;

public sealed record LogonTokenCreationRequest(
  Guid DeviceId,
  Guid TenantId,
  Guid? UserId,
  string? UserCorrelationId,
  string? UserDisplayName,
  string? SessionCorrelationId,
  int ExpirationMinutes,
  IReadOnlyList<InternalDtos.CredentialScopeDto>? Scopes,
  IReadOnlyList<int>? AllowedDesktopSessionIds,
  int SessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault)
{
  public const int MaxAllowedDesktopSessionIds = DtoLimits.AllowedDesktopSessionIdsMaxCount;
}
