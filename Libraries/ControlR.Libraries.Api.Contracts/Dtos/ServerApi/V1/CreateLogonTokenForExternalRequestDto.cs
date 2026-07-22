using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record CreateLogonTokenForExternalRequestDto(
  Guid DeviceId,
  Guid TenantId,
  [property: StringLength(252)]
  string UserCorrelationId,
  [property: StringLength(128)]
  string? UserDisplayName = null,
  [property: StringLength(128)]
  string? SessionCorrelationId = null,
  [property: Range(1, 1440)]
  int ExpirationMinutes = 15);
