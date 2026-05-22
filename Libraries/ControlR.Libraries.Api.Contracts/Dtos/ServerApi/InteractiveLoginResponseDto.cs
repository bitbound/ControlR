namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record InteractiveLoginResponseDto(
  bool RequiresTwoFactor,
  bool IsLockedOut = false,
  AccessTokenResponseDto? Tokens = null);