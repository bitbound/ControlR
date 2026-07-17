using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateLogonTokenForUserRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid UserId,
  [property: Range(1, 1440)]
  int ExpirationMinutes = 15);
