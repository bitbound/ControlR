namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record LogonTokenRequestDto(
  Guid DeviceId,
  int ExpirationMinutes = 15);