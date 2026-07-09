using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record CredentialPasswordChangeRequestDto(
  [Required]
  [EmailAddress]
  string Email,
  [Required]
  string CurrentPassword,
  [Required]
  [StringLength(100, MinimumLength = 8)]
  string NewPassword,
  string? TwoFactorCode = null);