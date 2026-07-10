namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

public record CreateServiceAccountCredentialResponseDto(
  ServiceAccountCredentialDto Credential,
  string PlainTextSecretKey);
