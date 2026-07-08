namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record IssueLogonTokenRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid UserId,
  int ExpirationMinutes = 15);
