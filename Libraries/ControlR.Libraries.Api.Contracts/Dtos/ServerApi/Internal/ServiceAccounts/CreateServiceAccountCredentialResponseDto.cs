namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.ServiceAccounts;

public record CreateServiceAccountCredentialResponseDto(
  ServiceAccountCredentialDto Credential,
  string PlainTextSecretKey);
