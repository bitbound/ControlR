using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

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

public record UpdatePermissionAssignmentRequestDto(
  [property: Required]
  [property: StringLength(150, MinimumLength = 1)]
  string PermissionName,

  PermissionEffect Effect,

  PermissionScopeKind ScopeKind,

  Guid? ScopeId,

  [property: StringLength(500)]
  string? Notes,

  bool IsEnabled);

public record DeleteManyPermissionAssignmentsRequestDto(
  [property: Required]
  [property: MaxLength(DtoLimits.PermissionAssignmentIdsMaxCount)]
  IReadOnlyList<Guid> AssignmentIds);

public record DeleteManyPermissionAssignmentsResponseDto(
  IReadOnlyList<Guid> SuccessIds,
  IReadOnlyList<Guid> FailureIds);

public record CreateManyPermissionAssignmentsRequestDto(
  [property: Required]
  [property: MinLength(1)]
  [property: MaxLength(DtoLimits.PermissionAssignmentIdsMaxCount)]
  IReadOnlyList<CreatePermissionAssignmentRequestDto> Assignments);

public record ReplacePermissionAssignmentsRequestDto(
  PermissionPrincipalKind PrincipalKind,

  Guid PrincipalId,

  [property: Required]
  [property: MinLength(1)]
  [property: MaxLength(DtoLimits.PermissionAssignmentIdsMaxCount)]
  IReadOnlyList<CreatePermissionAssignmentRequestDto> Assignments);

public record PermissionPresetDto(string Name, IReadOnlyList<string> Permissions);

public record ApplyPermissionPresetsRequestDto(
  PermissionPrincipalKind PrincipalKind,

  Guid PrincipalId,

  [property: Required]
  [property: MinLength(1)]
  IReadOnlyList<string> PresetNames,

  bool ReplaceExisting);
