using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ResetPasswordRequestDto(
  [Required]
  [EmailAddress]
  string Email,
  [Required]
  string ResetCode,
  [Required]
  [StringLength(100, MinimumLength = 8)]
  string NewPassword);