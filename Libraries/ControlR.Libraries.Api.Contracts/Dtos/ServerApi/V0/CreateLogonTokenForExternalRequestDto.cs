using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateLogonTokenForExternalRequestDto(
  Guid DeviceId,
  Guid TenantId,
  [StringLength(252)]
  string UserCorrelationId,
  [Range(1, 1440)]
  int ExpirationMinutes = 15);
