using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

/// <summary>Human-readable representation of a service account. Never exposes secret material.</summary>
public record ServiceAccountDto(
  Guid Id,
  string Name,
  string? Description,
  string Kind,
  bool IsEnabled,
  DateTimeOffset CreatedAt,
  IReadOnlyList<ServiceAccountCredentialDto> Credentials);

/// <summary>Metadata for a single service account credential. Never exposes the secret.</summary>
public record ServiceAccountCredentialDto(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? RevokedAt,
  DateTimeOffset? LastUsedAt);

public record CreateServiceAccountRequestDto(
  [Required]
  [StringLength(100, MinimumLength = 1)]
  string Name,
  [StringLength(500)]
  string? Description);

public record CreateServiceAccountResponseDto(
  ServiceAccountDto ServiceAccount,
  string PlainTextSecretKey);

public record CreateServiceAccountCredentialRequestDto(
  [Required]
  [StringLength(100, MinimumLength = 1)]
  string Name);

public record CreateServiceAccountCredentialResponseDto(
  ServiceAccountCredentialDto Credential,
  string PlainTextSecretKey);