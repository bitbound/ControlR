namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ServerLogonTokenRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid UserId,
  int ExpirationMinutes = 15);