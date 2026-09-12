namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record DeleteManyPermissionAssignmentsResponseDto(
  IReadOnlyList<Guid> SuccessIds,
  IReadOnlyList<Guid> FailureIds);