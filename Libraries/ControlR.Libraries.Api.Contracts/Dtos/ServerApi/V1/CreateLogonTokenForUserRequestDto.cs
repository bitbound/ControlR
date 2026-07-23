using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record CreateLogonTokenForUserRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid UserId,
  [property: StringLength(128)]
  string? SessionCorrelationId = null,
  [property: Range(1, 1440)]
  int ExpirationMinutes = 15);
