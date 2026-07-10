namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record LogonTokenResponseDto(
  Uri DeviceAccessUrl,
  DateTimeOffset ExpiresAt,
  string Token);
