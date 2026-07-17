using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record LoginRequestDto(
  [property: Required]
  [property: EmailAddress]
  string Email,
  [property: Required]
  string Password,
  string? TwoFactorCode = null,
  string? TwoFactorRecoveryCode = null);