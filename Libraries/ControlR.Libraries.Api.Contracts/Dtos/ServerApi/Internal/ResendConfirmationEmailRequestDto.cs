using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ResendConfirmationEmailRequestDto(
  [property: Required]
  [property: EmailAddress]
  string Email);