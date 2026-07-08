namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record IssueLogonTokenRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid? UserId,
  string? UserCorrelationId,
  LogonTokenKind Kind,
  int ExpirationMinutes = 15);
