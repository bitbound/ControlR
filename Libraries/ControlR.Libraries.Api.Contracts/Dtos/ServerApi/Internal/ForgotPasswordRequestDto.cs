using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ForgotPasswordRequestDto(
  [property: Required]
  [property: EmailAddress]
  string Email);