using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// The kind of resource a permission assignment is scoped to.
/// </summary>
/// <remarks>
/// Member names are the wire form on the V1 contract and the string persisted in
/// PermissionAssignments.ScopeKind, so renaming or reordering a member is a breaking change.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionScopeKind
{
  /// <summary>
  /// Sentinel default so an omitted scope kind never silently resolves to a real (and
  /// privileged) scope. No catalog permission allows this kind, so requests that omit the
  /// scope kind are rejected at validation.
  /// </summary>
  Unknown = 0,
  Server = 1,
  Tenant = 2,
  CustomerTenant = 3,
  DeviceGroup = 4,
  Device = 5,
  UserGroup = 6
}
