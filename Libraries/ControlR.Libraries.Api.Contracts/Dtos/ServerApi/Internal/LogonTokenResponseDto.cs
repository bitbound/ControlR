namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record LogonTokenResponseDto(
  Uri DeviceAccessUrl,
  DateTimeOffset ExpiresAt,
  string Token);
