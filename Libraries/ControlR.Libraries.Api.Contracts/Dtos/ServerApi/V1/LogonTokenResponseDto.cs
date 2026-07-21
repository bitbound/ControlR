namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record LogonTokenResponseDto(
  Uri DeviceAccessUrl,
  DateTimeOffset ExpiresAt,
  string Token);
