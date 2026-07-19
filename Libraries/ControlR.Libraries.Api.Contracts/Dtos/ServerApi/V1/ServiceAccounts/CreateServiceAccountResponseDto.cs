namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;

public record CreateServiceAccountResponseDto(
  ServiceAccountDto ServiceAccount,
  string PlainTextSecretKey);

