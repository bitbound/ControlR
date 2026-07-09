using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ResendConfirmationEmailRequestDto(
  [Required]
  [EmailAddress]
  string Email);