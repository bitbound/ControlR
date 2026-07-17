using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record LogonTokenRequestDto(
  Guid DeviceId,
  [property: Range(1, 1440)]
  int ExpirationMinutes = 15);