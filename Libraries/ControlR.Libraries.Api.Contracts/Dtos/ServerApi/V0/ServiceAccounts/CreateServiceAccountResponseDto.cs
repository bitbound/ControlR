namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

public record CreateServiceAccountResponseDto(
  ServiceAccountDto ServiceAccount,
  string PlainTextSecretKey);

