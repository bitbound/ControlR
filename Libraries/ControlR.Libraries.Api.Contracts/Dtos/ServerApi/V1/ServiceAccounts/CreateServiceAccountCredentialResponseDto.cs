namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;

public record CreateServiceAccountCredentialResponseDto(
  ServiceAccountCredentialDto Credential,
  string PlainTextSecretKey);

