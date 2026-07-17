using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record TwoFactorResponseDto(
  string SharedKey,
  int RecoveryCodesLeft,
  string[]? RecoveryCodes,
  bool IsTwoFactorEnabled,
  bool IsMachineRemembered);