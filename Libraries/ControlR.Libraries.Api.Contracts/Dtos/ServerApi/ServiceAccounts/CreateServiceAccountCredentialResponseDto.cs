namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.ServiceAccounts;

public record CreateServiceAccountCredentialResponseDto(
  ServiceAccountCredentialDto Credential,
  string PlainTextSecretKey);
