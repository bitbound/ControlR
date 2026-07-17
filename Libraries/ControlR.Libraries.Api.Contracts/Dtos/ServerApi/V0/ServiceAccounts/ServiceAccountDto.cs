namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

public record ServiceAccountDto(
  Guid Id,
  string Name,
  string? Description,
  string Kind,
  bool IsEnabled,
  DateTimeOffset CreatedAt,
  IReadOnlyList<ServiceAccountCredentialDto> Credentials);

