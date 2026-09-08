using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record CreateServerServiceAccountRequestDto(
  [property: Required]
  [property: StringLength(100, MinimumLength = 1)]
  string Name,

  [property: StringLength(500)]
  string? Description,

  [property: Required]
  ServiceAccountAccessMode AccessMode);

public record UpdateServerServiceAccountRequestDto(
  [property: Required]
  [property: StringLength(100, MinimumLength = 1)]
  string Name,

  [property: StringLength(500)]
  string? Description,

  bool IsEnabled);

public record CreateServerServiceAccountCredentialRequestDto(
  [property: Required]
  [property: StringLength(100, MinimumLength = 1)]
  string Name,

  DateTimeOffset? ExpiresAt = null);

public record ServerServiceAccountCredentialDto(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? RevokedAt,
  DateTimeOffset? LastUsedAt);

public record ServerServiceAccountDto(
  Guid Id,
  string Name,
  string? Description,
  bool IsEnabled,
  ServiceAccountAccessMode AccessMode,
  DateTimeOffset CreatedAt,
  IReadOnlyList<ServerServiceAccountCredentialDto> Credentials);

public record CreateServerServiceAccountCredentialResponseDto(
  ServerServiceAccountCredentialDto Credential,
  string PlainTextSecretKey);
