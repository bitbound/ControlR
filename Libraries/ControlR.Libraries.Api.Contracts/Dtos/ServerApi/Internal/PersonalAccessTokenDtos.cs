using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record PersonalAccessTokenResponseDto(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? LastUsed);

public record CreatePersonalAccessTokenRequestDto(
  [property: Required]
  [property: StringLength(256, MinimumLength = 1)]
  string Name);

public record CreatePersonalAccessTokenResponseDto(
  PersonalAccessTokenResponseDto PersonalAccessToken,
  string PlainTextToken);

public record UpdatePersonalAccessTokenRequestDto(
  [property: Required]
  [property: StringLength(256, MinimumLength = 1)]
  string Name);
