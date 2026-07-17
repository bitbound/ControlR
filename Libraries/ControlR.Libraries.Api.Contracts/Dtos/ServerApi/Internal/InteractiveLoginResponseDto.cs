namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record InteractiveLoginResponseDto(
  bool RequiresTwoFactor,
  bool IsLockedOut = false,
  bool RequiresPasswordChange = false,
  AccessTokenResponseDto? Tokens = null);