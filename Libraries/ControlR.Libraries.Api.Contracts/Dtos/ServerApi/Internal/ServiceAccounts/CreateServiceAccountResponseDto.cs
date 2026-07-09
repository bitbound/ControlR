namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.ServiceAccounts;

public record CreateServiceAccountResponseDto(
  ServiceAccountDto ServiceAccount,
  string PlainTextSecretKey);
