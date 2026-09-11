namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record PermissionPresetDto(string Name, IReadOnlyList<string> Permissions);