using System.Collections.Immutable;

namespace ControlR.Libraries.Api.Contracts.Authz;

/// <summary>
/// The permissions that satisfy a policy and the canonical resource kind it evaluates against.
/// A policy with more than one permission succeeds when any listed permission is allowed, which
/// is how the installer-key pair grants one shared route to both the self and the others holders.
/// The names are an immutable snapshot so the process-wide policy table cannot be mutated through
/// the record and definitions compare by value.
/// </summary>
public sealed record PermissionPolicyDefinition(
  ImmutableArray<string> PermissionNames,
  PermissionScopeKind ResourceScopeKind)
{
  public PermissionPolicyDefinition(
    string permissionName,
    PermissionScopeKind resourceScopeKind)
    : this([permissionName], resourceScopeKind)
  {
  }
}
