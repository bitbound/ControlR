using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ResendConfirmationEmailRequestDto(
  [Required]
  [EmailAddress]
  string Email);