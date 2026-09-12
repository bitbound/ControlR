namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public class PermissionCatalogResponseDto
{
  public IReadOnlyList<PermissionCatalogEntryDto> Items { get; set; } = [];
}