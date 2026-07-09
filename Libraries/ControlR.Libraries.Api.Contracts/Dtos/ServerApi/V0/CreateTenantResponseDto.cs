namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record CreateTenantResponseDto(
  Guid TenantId,
  string TenantName);