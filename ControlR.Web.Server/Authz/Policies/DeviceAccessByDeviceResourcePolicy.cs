using ControlR.Web.Client.Extensions;

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

        if (handlerCtx.User.IsInRole(RoleNames.TenantAdministrator) ||
            handlerCtx.User.IsInRole(RoleNames.DeviceSuperUser))
        {
          return true;
        }

        if (!handlerCtx.User.TryGetUserId(out var userId))
        {
          return Fail("User ID claim is missing.", handlerCtx, authzHandler, logger);
        }

        await using var scope = sp.CreateAsyncScope();
        await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await db.Users
          .AsNoTracking()
          .Include(x => x.Tags)
          .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
        {
          return Fail("User does not exist.", handlerCtx, authzHandler, logger);
        }

        // For logon token users, check if the device ID matches the token's device
        var authMethod = handlerCtx.User.FindFirst(UserClaimTypes.AuthenticationMethod)?.Value;
        if (authMethod == "LogonToken")
        {
          var deviceIdClaim = handlerCtx.User.FindFirst(UserClaimTypes.DeviceId)?.Value;
          if (deviceIdClaim != null && Guid.TryParse(deviceIdClaim, out var tokenDeviceId))
          {
            if (device.Id == tokenDeviceId)
            {
              logger.LogInformation(
                "Logon token user {UserId} authorized for device {DeviceId}",
                userId, device.Id);
              return true;
            }
          }
          
          return Fail(
            "Logon token is not authorized for this device.",
            handlerCtx,
            authzHandler,
            logger);
        }

        await db
          .Update(device)
          .Collection(x => x.Tags!)
          .LoadAsync();

        if (user.Tags is null ||
            device.Tags is null ||
            !user.Tags.Any(x => device.Tags.Exists(y => y.Id == x.Id)))
        {
          return Fail(
            $"User {user.UserName} is not authorized to access device {device.Name} (ID: {device.Id}).",
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
    logger.LogInformation("Authorization failure: {Reason}", reason);
    return false;
  }
}