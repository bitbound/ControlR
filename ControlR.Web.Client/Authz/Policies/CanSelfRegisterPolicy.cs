namespace ControlR.Web.Client.Authz.Policies;

public static class CanSelfRegisterPolicy
{
  public const string PolicyName = "CanSelfRegisterPolicy";

  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireClaim(UserClaimTypes.CanSelfRegister)
      .Build();
  }
}
