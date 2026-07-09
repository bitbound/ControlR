using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record AccessTokenResponseDto(
  [Required]
  string TokenType,
  [Required]
  string AccessToken,
  int ExpiresInSeconds,
  [Required]
  string RefreshToken);