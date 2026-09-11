using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record ApplyPermissionPresetsRequestDto(
  PermissionPrincipalKind PrincipalKind,

  Guid PrincipalId,

  [property: Required]
  [property: MinLength(1)]
  IReadOnlyList<string> PresetNames,

  bool ReplaceExisting);