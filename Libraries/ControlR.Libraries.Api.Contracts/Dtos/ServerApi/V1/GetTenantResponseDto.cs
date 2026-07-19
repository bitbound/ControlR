namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record GetTenantResponseDto(
  Guid TenantId,
  string TenantName);