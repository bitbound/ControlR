using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ResetPasswordRequestDto(
  [property: Required]
  [property: EmailAddress]
  string Email,
  [property: Required]
  string ResetCode,
  [property: Required]
  [property: StringLength(100, MinimumLength = 8)]
  string NewPassword);