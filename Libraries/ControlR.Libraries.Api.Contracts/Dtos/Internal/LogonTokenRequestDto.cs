namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record LogonTokenRequestDto(
  Guid DeviceId,
  int ExpirationMinutes = 15);