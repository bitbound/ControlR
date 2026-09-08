namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record TwoFactorResponseDto(
  string SharedKey,
  int RecoveryCodesLeft,
  IReadOnlyList<string>? RecoveryCodes,
  bool IsTwoFactorEnabled,
  bool IsMachineRemembered);