using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Authz.Policies;

public static class RemoteControlByDevicePolicy
{
  public const string PolicyName = "RemoteControlByDevice";

  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireServiceProviderAssertion(async (sp, handlerCtx) =>
      {
        if (handlerCtx.Resource is not Device device)
        {
          return false;
        }

        // TODO
        return true;
      })
      .Build();
  }
}
