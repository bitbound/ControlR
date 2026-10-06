namespace ControlR.Web.Server.Authz.Permissions;

/// <summary>
/// One authorization check: allow if any listed permission is allowed against the resource.
/// Policies name a single permission; a policy names several only when one shared route must
/// admit holders of distinct permissions, such as the installer-key self and others pair.
/// </summary>
public sealed class PermissionRequirement(
  IReadOnlyList<string> permissionNames,
  ResourceDescriptor resource)
  : IAuthorizationRequirement
{
  public PermissionRequirement(
    string permissionName,
    ResourceDescriptor resource)
    : this([permissionName], resource)
  {
  }

  public IReadOnlyList<string> PermissionNames { get; } = permissionNames;
  public ResourceDescriptor Resource { get; } = resource;
}
