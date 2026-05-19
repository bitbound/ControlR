namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record DesktopLoginResponseDto(
  bool RequiresTwoFactor,
  AccessTokenResponseDto? Tokens = null);