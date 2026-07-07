namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record CreateTenantResponseDto(
  Guid TenantId,
  string TenantName,
  Guid UserId,
  string UserName,
  string? Email);