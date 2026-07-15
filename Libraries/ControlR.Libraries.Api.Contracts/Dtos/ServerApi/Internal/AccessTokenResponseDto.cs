using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record AccessTokenResponseDto(
  string TokenType,
  string AccessToken,
  int ExpiresInSeconds,
  string RefreshToken);