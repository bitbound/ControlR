using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record RegisterRequestDto(
  [Required]
  [EmailAddress]
  string Email,
  [Required]
  string Password);