using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record ReplacePermissionAssignmentsRequestDto(
  PermissionPrincipalKind PrincipalKind,

  Guid PrincipalId,

  [property: Required]
  [property: MinLength(1)]
  [property: MaxLength(DtoLimits.PermissionAssignmentIdsMaxCount)]
  IReadOnlyList<CreatePermissionAssignmentRequestDto> Assignments);