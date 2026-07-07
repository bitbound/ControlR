namespace ControlR.Web.Server.Authz.Policies;

public static class RequireUserPrincipalPolicy
{
  public const string PolicyName = "RequireUserPrincipalPolicy";

  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireAssertion(context =>
        context.User.HasClaim(c => c.Type == UserClaimTypes.TenantId) &&
        context.User.HasClaim(c => c.Type == UserClaimTypes.UserId))
      .Build();
  }
}
