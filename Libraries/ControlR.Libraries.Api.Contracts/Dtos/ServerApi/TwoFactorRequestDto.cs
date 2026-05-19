namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record TwoFactorRequestDto(
  bool? Enable = null,
  string? TwoFactorCode = null,
  bool? ResetSharedKey = null,
  bool? ResetRecoveryCodes = null,
  bool? ForgetMachine = null);