using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record LoginRequestDto(
  [Required]
  [EmailAddress]
  string Email,
  [Required]
  string Password,
  string? TwoFactorCode = null,
  string? TwoFactorRecoveryCode = null);