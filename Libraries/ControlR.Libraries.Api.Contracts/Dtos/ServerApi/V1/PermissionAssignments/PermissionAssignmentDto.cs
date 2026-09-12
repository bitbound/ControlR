namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record PermissionAssignmentDto(
  Guid Id,
  PermissionPrincipalKind PrincipalKind,
  Guid PrincipalId,
  string PermissionName,
  PermissionEffect Effect,
  PermissionScopeKind ScopeKind,
  Guid? ScopeId,
  string? Notes,
  bool IsEnabled,
  DateTimeOffset CreatedAt);