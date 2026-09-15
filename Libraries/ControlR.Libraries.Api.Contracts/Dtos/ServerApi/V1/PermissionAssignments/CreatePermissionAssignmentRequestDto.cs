using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record CreatePermissionAssignmentRequestDto(
  PermissionPrincipalKind PrincipalKind,

  Guid PrincipalId,

  [property: Required]
  [property: StringLength(150, MinimumLength = 1)]
  string PermissionName,

  PermissionEffect Effect,

  PermissionScopeKind ScopeKind,

  Guid? ScopeId,

  [property: StringLength(500)]
  string? Notes,

  bool IsEnabled = true);