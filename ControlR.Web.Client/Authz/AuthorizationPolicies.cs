using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Client.Auth;

public static class AuthorizationPolicies
{
  public static AuthorizationPolicy RequireServerAdministrator =>
    new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireRole(RoleNames.ServerAdministrator)
      .Build();
}