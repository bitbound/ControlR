namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record CreateTenantResponseDto(
  Guid TenantId,
  string TenantName);