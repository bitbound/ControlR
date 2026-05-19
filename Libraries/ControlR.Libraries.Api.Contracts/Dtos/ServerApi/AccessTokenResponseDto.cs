using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record AccessTokenResponseDto(
  [Required]
  string TokenType,
  [Required]
  string AccessToken,
  int ExpiresIn,
  [Required]
  string RefreshToken);