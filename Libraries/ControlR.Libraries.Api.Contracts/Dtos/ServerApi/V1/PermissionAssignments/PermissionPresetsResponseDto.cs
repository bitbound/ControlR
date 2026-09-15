namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public class PermissionPresetsResponseDto
{
  public IReadOnlyList<PermissionPresetDto> Items { get; set; } = [];
}