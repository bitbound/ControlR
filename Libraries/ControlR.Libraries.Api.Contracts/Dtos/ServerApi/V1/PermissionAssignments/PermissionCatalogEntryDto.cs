namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

public record PermissionCatalogEntryDto(
  string Name,
  string DisplayName,
  string CategoryLabel,
  string Description,
  IReadOnlyList<PermissionScopeKind> AllowedScopeKinds,
  bool SelfRemovable);