namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

/// <summary>
/// Minimal tenant projection for the collection listing. The full tenant object is
/// returned by <c>GET /api/v1/tenants/{tenantId}</c>.
/// </summary>
public record TenantSummaryDto(
  Guid Id,
  string Name);
