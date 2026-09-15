namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public class TenantsResponseDto
{
  public IReadOnlyList<TenantSummaryDto> Items { get; set; } = [];
}
