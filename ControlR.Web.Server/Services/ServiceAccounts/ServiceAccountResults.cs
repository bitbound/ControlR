namespace ControlR.Web.Server.Services.ServiceAccounts;

/// <summary>
/// Business-layer representation of a service account, decoupled from API DTOs.
/// Controllers map this to the appropriate DTO (V1 or Internal) at the boundary.
/// </summary>
public sealed record ServiceAccountResult(
  Guid Id,
  string Name,
  string? Description,
  ServiceAccountKind Kind,
  bool IsEnabled,
  /// <summary>
  /// The access mode. Always set (non-null) for server-scoped accounts.
  /// <see cref="ServiceAccountAccessMode.Restricted"/> is the sentinel value used for
  /// tenant-scoped accounts, which are not governed by the mode and never bypass.
  /// </summary>
  ServiceAccountAccessMode AccessMode,
  DateTimeOffset CreatedAt,
  IReadOnlyList<ServiceAccountCredentialResult> Credentials);

/// <summary>
/// Business-layer representation of a service account credential.
/// </summary>
public sealed record ServiceAccountCredentialResult(
  Guid Id,
  string Name,
  DateTimeOffset CreatedAt,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? RevokedAt,
  DateTimeOffset? LastUsedAt);

/// <summary>
/// Returned when a new credential is added to a service account. Includes the
/// plaintext secret which is only available at creation time.
/// </summary>
public sealed record CreateServiceAccountCredentialResult(
  ServiceAccountCredentialResult Credential,
  string PlainTextSecretKey);
