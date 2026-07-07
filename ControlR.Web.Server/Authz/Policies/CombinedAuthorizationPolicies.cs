namespace ControlR.Web.Server.Authz.Policies;

public static class CombinedAuthorizationPolicies
{
  public const string RequireServerOrTenantAdminOrInstallerKeyManagerPolicy = nameof(RequireServerOrTenantAdminOrInstallerKeyManagerPolicy);
  public const string RequireServerOrTenantAdminPolicy = nameof(RequireServerOrTenantAdminPolicy);

  public static AuthorizationPolicy CreateServerOrTenantAdmin()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireAssertion(context =>
        context.User.IsServerPrincipal() ||
        context.User.IsInRole(RoleNames.TenantAdministrator))
      .Build();
  }

  public static AuthorizationPolicy CreateServerOrTenantAdminOrInstallerKeyManager()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireAssertion(context =>
        context.User.IsServerPrincipal() ||
        context.User.IsInRole(RoleNames.TenantAdministrator) ||
        context.User.IsInRole(RoleNames.InstallerKeyManager))
      .Build();
  }
}