using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record PersonalAccessTokenDto(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? LastUsed);

public record CreatePersonalAccessTokenRequestDto(
  [Required]
  [StringLength(256, MinimumLength = 1)]
  string Name);

public record CreatePersonalAccessTokenResponseDto(
  PersonalAccessTokenDto PersonalAccessToken,
  string PlainTextToken);

public record UpdatePersonalAccessTokenRequestDto(
  [Required]
  [StringLength(256, MinimumLength = 1)]
  string Name);
