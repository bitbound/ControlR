using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record ForgotPasswordRequestDto(
  [Required]
  [EmailAddress]
  string Email);