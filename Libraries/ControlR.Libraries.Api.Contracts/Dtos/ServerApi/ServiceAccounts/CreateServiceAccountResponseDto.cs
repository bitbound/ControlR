namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.ServiceAccounts;

public record CreateServiceAccountResponseDto(
  ServiceAccountDto ServiceAccount,
  string PlainTextSecretKey);
