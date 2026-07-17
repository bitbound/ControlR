using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateLogonTokenForExternalRequestDto(
  Guid DeviceId,
  Guid TenantId,
  [property: StringLength(252)]
  string UserCorrelationId,
  [property: Range(1, 1440)]
  int ExpirationMinutes = 15);
