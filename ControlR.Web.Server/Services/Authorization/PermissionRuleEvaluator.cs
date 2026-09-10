using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization.PermissionRules;

namespace ControlR.Web.Server.Services.Authorization;

public static class PermissionRuleEvaluator
{
  public static PermissionEvaluationResult Evaluate(
    PermissionEvaluationContext context,
    string permissionName,
    ResourceDescriptor resource)
  {
    if (!PermissionCatalog.Exists(permissionName))
    {
      return PermissionEvaluationResult.Deny($"Unknown permission '{permissionName}'.");
    }

    if (context.ServerBypass)
    {
      return PermissionEvaluationResult.Allow("server-service-account-bypass", "Server");
    }

    var principal = context.Principal;
    if (principal.CredentialType == CredentialType.LogonToken)
    {
      if (!principal.DeviceScopeId.HasValue)
      {
        return PermissionEvaluationResult.Deny("Logon token principal is missing required device scope.");
      }

      if (!principal.CredentialId.HasValue)
      {
        return PermissionEvaluationResult.Deny("Logon token principal is missing required credential id.");
      }

      if (resource.Kind == PermissionScopeKind.Device &&
          resource.Id.HasValue &&
          resource.Id.Value != principal.DeviceScopeId.Value)
      {
        return PermissionEvaluationResult.Deny("Logon token session is restricted to its scoped device.");
      }

      if (context.EffectiveRules.Count == 0)
      {
        return PermissionEvaluationResult.Deny("Logon token grants do not match the device scope.");
      }
    }

    if (context.HasExplicitPatScope)
    {
      var ownerCheck = EvaluateRules(context.OwnerRules, permissionName, resource);
      if (!ownerCheck.Allowed)
      {
        return PermissionEvaluationResult.Deny(
          "Credential scope grants are outside the user's effective permissions.");
      }

      return EvaluateRules(context.EffectiveRules, permissionName, resource);
    }

    return EvaluateRules(context.EffectiveRules, permissionName, resource);
  }

  public static PermissionEvaluationResult EvaluateRules(
    IReadOnlyList<PermissionRule> rules,
    string permissionName,
    ResourceDescriptor resource)
  {
    if (!PermissionCatalog.Exists(permissionName))
    {
      return PermissionEvaluationResult.Deny($"Unknown permission '{permissionName}'.");
    }

    var metadata = PermissionCatalog.Get(permissionName);

    // Scope-legality applies to ALLOW rules only: an allow at a scope the permission does not
    // permit must not grant access (fail closed, drop it). DENY rules are always kept, since a
    // deny at any scope is a security restriction that must be honored (dropping it would fail
    // open). See scope-legality PRD.
    var matchingRules = rules
      .Where(rule => rule.PermissionName == permissionName &&
                     (rule.Effect == PermissionEffect.Deny ||
                      metadata is null || metadata.AllowedScopeKinds.Contains(rule.ScopeKind)) &&
                     PermissionScopeMatcher.Matches(rule, resource))
      .ToList();

    if (matchingRules.Count == 0)
    {
      return PermissionEvaluationResult.Deny("No matching permission assignment found (default deny).");
    }

    var denyRule = matchingRules
      .Where(rule => rule.Effect == PermissionEffect.Deny)
      .OrderBy(rule => rule.Priority)
      .ThenBy(rule => ScopeSpecificity(rule.ScopeKind))
      .FirstOrDefault();

    if (denyRule is not null)
    {
      return PermissionEvaluationResult.Deny(
        $"Explicit deny from {denyRule.Source} at scope {denyRule.ScopeKind}.");
    }

    var allowRule = matchingRules
      .Where(rule => rule.Effect == PermissionEffect.Allow)
      .OrderBy(rule => rule.Priority)
      .ThenBy(rule => ScopeSpecificity(rule.ScopeKind))
      .FirstOrDefault();

    return allowRule is null
      ? PermissionEvaluationResult.Deny("No matching allow rule found (default deny).")
      : PermissionEvaluationResult.Allow(
          allowRule.Source.ToString(), allowRule.ScopeKind.ToString());
  }

  private static int ScopeSpecificity(PermissionScopeKind scopeKind) => scopeKind switch
  {
    PermissionScopeKind.Device => 0,
    PermissionScopeKind.DeviceGroup => 1,
    PermissionScopeKind.CustomerTenant => 2,
    PermissionScopeKind.UserGroup => 3,
    PermissionScopeKind.Tenant => 4,
    PermissionScopeKind.Server => 5,
    _ => 6
  };
}
