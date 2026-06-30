using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services.DeviceManagement;

namespace ControlR.Web.Server.Authz.Policies;

public static class DeviceAccessByDeviceResourcePolicy
{
  public const string PolicyName = "DeviceAccessByDeviceResourcePolicy";

  public static AuthorizationPolicy Create()
  {
    return new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .RequireServiceProviderAssertion(async (sp, handlerCtx, authzHandler) =>
      {
        // Server-scoped service accounts have unbound (cross-tenant) access in the interim
        // authorization model. The full permission evaluator (Phase 2 PR 11) replaces this
        // bypass with explicit server permission checks.
        if (handlerCtx.User.IsServerPrincipal())
        {
          handlerCtx.Succeed(handlerCtx.Requirements.OfType<IAuthorizationRequirement>().First());
          return true;
        }

        var logger = sp.GetRequiredService<ILogger<AuthorizationPolicyBuilder>>();

        if (handlerCtx.Resource is not Device device)
        {
          return Fail("Resource must be a device.", handlerCtx, authzHandler, logger);
        }

        if (!handlerCtx.User.TryGetTenantId(out var tenantId))
        {
          return Fail("Tenant ID claim is missing.", handlerCtx, authzHandler, logger);
        }

        if (device.TenantId != tenantId)
        {
          return Fail("Device does not belong to this tenant.", handlerCtx, authzHandler, logger);
        }

        if (!handlerCtx.User.TryGetUserId(out var userId))
        {
          return Fail("User ID claim is missing.", handlerCtx, authzHandler, logger);
        }

        var accessScopeResolver = sp.GetRequiredService<IDeviceAccessScopeResolver>();
        var accessScope = await accessScopeResolver.Resolve(handlerCtx.User, tenantId);

        if (accessScope.Kind == DeviceAccessScopeKind.SingleDevice)
        {
          if (accessScope.DeviceId == device.Id)
          {
            logger.LogInformation(
              "Logon token user {UserId} authorized for scoped device {DeviceId}",
              userId, device.Id);
            return true;
          }

          return Fail(
            $"Logon token user {userId} is not authorized for device {device.Id}.",
            handlerCtx,
            authzHandler,
            logger);
        }

        if (accessScope.Kind == DeviceAccessScopeKind.TenantWide)
        {
          return true;
        }

        if (accessScope.Kind == DeviceAccessScopeKind.None)
        {
          return Fail("User does not have access tags.", handlerCtx, authzHandler, logger);
        }

        var db = sp.GetRequiredService<AppDb>();
        var entry = db.Entry(device);
        if (entry.State == EntityState.Detached)
        {
          db.Attach(device);
          entry = db.Entry(device);
        }

        await entry.Collection(x => x.Tags!).LoadAsync();

        if (device.Tags is null ||
            !device.Tags.Any(x => accessScope.TagIds.Contains(x.Id)))
        {
          return Fail(
            $"User {userId} is not authorized to access device {device.Name} (ID: {device.Id}).",
            handlerCtx,
            authzHandler,
            logger);
        }

        return true;
      })
      .Build();
  }

  private static bool Fail(
    string reason,
    AuthorizationHandlerContext handlerCtx,
    IAuthorizationHandler authzHandler,
    ILogger<AuthorizationPolicyBuilder> logger)
  {
    var authorizationResult = new AuthorizationFailureReason(authzHandler, reason);
    handlerCtx.Fail(authorizationResult);
    logger.LogDebug("Authorization failure: {Reason}", reason);
    return false;
  }
}