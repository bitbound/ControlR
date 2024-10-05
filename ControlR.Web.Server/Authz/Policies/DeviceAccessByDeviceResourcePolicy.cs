using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Authz.Policies;

public static class DeviceAccessByDeviceResourcePolicy
{
  public const string PolicyName = "DeviceAccessByDeviceResourcePolicy";

  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireServiceProviderAssertion((sp, handlerCtx) =>
      {
        if (handlerCtx.Resource is not Device device)
        {
          return false;
        }

        if (!handlerCtx.User.TryGetTenantId(out var tenantId))
        {
          return false;
        }

        if (device.TenantId != tenantId)
        {
          return false;
        }

        if (handlerCtx.User.IsInRole(RoleNames.ServerAdministrator) ||
          handlerCtx.User.IsInRole(RoleNames.DeviceAdministrator))
        {
          return true;
        }

        handlerCtx.Fail();
        return false;
      })
      .Build();
  }
}
