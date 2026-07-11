using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateLogonTokenRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid? UserId,
  [StringLength(512)]
  string? UserCorrelationId,
  LogonTokenKind Kind,
  [Range(1, 1440)]
  int ExpirationMinutes = 15);
