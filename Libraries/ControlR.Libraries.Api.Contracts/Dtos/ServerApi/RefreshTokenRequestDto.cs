using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record RefreshTokenRequestDto(
  [Required]
  string RefreshToken);