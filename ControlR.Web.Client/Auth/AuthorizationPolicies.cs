using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Client.Auth;

public static class AuthorizationPolicies
{
  public static AuthorizationPolicy RequireAdministrator =>
    new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireClaim(ClaimNames.IsAdministrator)
      .Build();
}