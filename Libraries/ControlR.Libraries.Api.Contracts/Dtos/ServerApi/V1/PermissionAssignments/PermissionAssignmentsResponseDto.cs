namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public class PermissionAssignmentsResponseDto
{
  public IReadOnlyList<PermissionAssignmentDto> Items { get; set; } = [];
}