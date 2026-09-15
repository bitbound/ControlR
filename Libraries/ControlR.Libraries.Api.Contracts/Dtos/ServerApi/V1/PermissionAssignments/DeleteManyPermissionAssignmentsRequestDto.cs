using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record DeleteManyPermissionAssignmentsRequestDto(
  [property: Required]
  [property: MaxLength(DtoLimits.PermissionAssignmentIdsMaxCount)]
  IReadOnlyList<Guid> AssignmentIds);