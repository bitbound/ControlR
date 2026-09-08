namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;

/// <summary>
/// V1 response for a server-scoped service account. Always carries a non-null
/// <see cref="AccessMode"/>, which is set explicitly at creation and never inferred.
/// </summary>
public record ServerServiceAccountDto(
  Guid Id,
  string Name,
  string? Description,
  bool IsEnabled,
  ServiceAccountAccessMode AccessMode,
  DateTimeOffset CreatedAt,
  IReadOnlyList<ServiceAccountCredentialDto> Credentials);
