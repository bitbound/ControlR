using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record TwoFactorResponseDto(
  [Required]
  string SharedKey,
  int RecoveryCodesLeft,
  string[]? RecoveryCodes,
  bool IsTwoFactorEnabled,
  bool IsMachineRemembered);