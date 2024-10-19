namespace ControlR.Web.Client.Authz.Policies;

public static class RequireServerAdministratorPolicy
{
  public const string PolicyName = "RequireServerAdministratorPolicy";
  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireRole(RoleNames.ServerAdministrator)
      .Build();
  }
}