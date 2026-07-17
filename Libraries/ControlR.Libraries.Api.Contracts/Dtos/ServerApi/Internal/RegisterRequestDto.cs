using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record RegisterRequestDto(
  [property: Required]
  [property: EmailAddress]
  string Email,
  [property: Required]
  string Password);