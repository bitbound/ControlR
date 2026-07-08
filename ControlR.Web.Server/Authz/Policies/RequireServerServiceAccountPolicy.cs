namespace ControlR.Web.Server.Authz.Policies;

public static class RequireServerServiceAccountPolicy
{
  public const string PolicyName = "RequireServerServiceAccountPolicy";

  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireAssertion(context => context.User.IsServerPrincipal() || context.User.IsInRole(RoleNames.ServerAdministrator))
      .Build();
  }
}