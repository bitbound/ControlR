namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record GetTenantResponseDto(
  Guid TenantId,
  string TenantName);