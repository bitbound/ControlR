using System.ComponentModel.DataAnnotations;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateLogonTokenRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid? UserId,
  [StringLength(512)]
  string? UserCorrelationId,
  LogonTokenKind Kind,
  int ExpirationMinutes = 15);
