namespace ControlR.Libraries.Api.Contracts.Dtos.V0;

public record CreateTenantResponseDto(
  Guid TenantId,
  string TenantName,
  Guid UserId,
  string UserName,
  string? Email);