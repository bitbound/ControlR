using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record LogonTokenRequestDto(
  Guid DeviceId,
  [property: Range(DtoLimits.ExpirationMinutesMin, DtoLimits.ExpirationMinutesMax)]
  int ExpirationMinutes = DtoLimits.ExpirationMinutesDefault,
  IReadOnlyList<CredentialScopeDto>? Scopes = null,
  [property: Range(DtoLimits.SessionExpirationMinutesMin, DtoLimits.SessionExpirationMinutesMax)]
  int SessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault);
