using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record CredentialPasswordChangeRequestDto(
  [property: Required]
  [property: EmailAddress]
  string Email,
  [property: Required]
  string CurrentPassword,
  [property: Required]
  [property: StringLength(100, MinimumLength = 8)]
  string NewPassword,
  string? TwoFactorCode = null);