using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Authz;

public static class AuthorizationPolicyBuilderExtensions
{

  /// <summary>
  /// Adds a <see cref="PermissionRequirement"/> satisfied when any listed permission is allowed.
  /// Used for policies that guard one shared route for holders of distinct permissions, such as
  /// the installer-key self and others pair.
  /// </summary>
  public static AuthorizationPolicyBuilder RequireAnyPermission(
    this AuthorizationPolicyBuilder builder,
    IReadOnlyList<string> permissionNames,
    PermissionScopeKind scopeKind = PermissionScopeKind.Tenant)
  {
    builder.Requirements.Add(new PermissionRequirement(
      permissionNames, new ResourceDescriptor(scopeKind)));
    return builder;
  }

  /// <summary>
  /// Adds a <see cref="PermissionRequirement"/> to the policy. The requirement delegates
  /// to the centralized permission evaluator at authorization time.
  /// </summary>
  public static AuthorizationPolicyBuilder RequirePermission(
    this AuthorizationPolicyBuilder builder,
    string permissionName,
    PermissionScopeKind scopeKind = PermissionScopeKind.Tenant)
  {
    builder.Requirements.Add(new PermissionRequirement(
      permissionName, new ResourceDescriptor(scopeKind)));
    return builder;
  }
}