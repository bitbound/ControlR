using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record CreateLogonTokenForUserRequestDto(
  Guid DeviceId,
  Guid TenantId,
  Guid UserId,
  [property: StringLength(DtoLimits.SessionCorrelationIdMaxLength)]
  string? SessionCorrelationId = null,
  [property: Range(DtoLimits.ExpirationMinutesMin, DtoLimits.ExpirationMinutesMax)]
  int ExpirationMinutes = DtoLimits.ExpirationMinutesDefault,
  [property: MaxLength(DtoLimits.PermissionsMaxLength)]
  IReadOnlyList<string>? Permissions = null,
  [property: MaxLength(DtoLimits.AllowedDesktopSessionIdsMaxCount)]
  IReadOnlyList<int>? AllowedDesktopSessionIds = null,
  [property: Range(DtoLimits.SessionExpirationMinutesMin, DtoLimits.SessionExpirationMinutesMax)]
  int SessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault);
