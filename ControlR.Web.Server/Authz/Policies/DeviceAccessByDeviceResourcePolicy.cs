using ControlR.Web.Client.Extensions;

namespace ControlR.Web.Server.Authz.Policies;

public static class DeviceAccessByDeviceResourcePolicy
{
  public const string PolicyName = "DeviceAccessByDeviceResourcePolicy";

  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireServiceProviderAssertion(async (sp, handlerCtx) =>
      {
        var logger = sp.GetRequiredService<ILogger<AuthorizationPolicyBuilder>>();

        if (handlerCtx.Resource is not Device device)
        {
          handlerCtx.Fail();
          return false;
        }

        if (!handlerCtx.User.TryGetTenantId(out var tenantId))
        {
          handlerCtx.Fail();
          return false;
        }

        if (device.TenantId != tenantId)
        {
          handlerCtx.Fail();
          return false;
        }

        if (handlerCtx.User.IsInRole(RoleNames.TenantAdministrator))
        {
          return true;
        }

        await using var scope = sp.CreateAsyncScope();
        using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var tenant = await db.Tenants
          .AsNoTracking()
          .FirstOrDefaultAsync(x => x.Id == tenantId);

        if (tenant is null)
        {
          handlerCtx.Fail();
          return false;
        }


        handlerCtx.Fail();
        return false;
      })
      .Build();
  }
}
