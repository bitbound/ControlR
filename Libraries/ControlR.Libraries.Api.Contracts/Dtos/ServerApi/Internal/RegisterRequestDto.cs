using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record RegisterRequestDto(
  [Required]
  [EmailAddress]
  string Email,
  [Required]
  string Password);