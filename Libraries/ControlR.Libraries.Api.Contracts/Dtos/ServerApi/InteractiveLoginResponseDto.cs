namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record InteractiveLoginResponseDto(
  bool RequiresTwoFactor,
  AccessTokenResponseDto? Tokens = null);