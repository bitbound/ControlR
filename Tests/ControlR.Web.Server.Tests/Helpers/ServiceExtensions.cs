using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Services;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Tests.Helpers;

internal static class ServiceExtensions
{
  /// <summary>
  /// Creates an instance of a controller with the necessary services injected from a service scope
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="scope">The service scope to use for dependency resolution</param>
  /// <returns>An instance of the controller</returns>
  public static T CreateController<T>(this IServiceScope scope) where T : ControllerBase
  {
    var controller = ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider);
    controller.ControllerContext = new ControllerContext
    {
      HttpContext = new DefaultHttpContext
      {
        RequestServices = scope.ServiceProvider
      }
    };
    return controller;
  }

  /// <summary>
  /// Creates a test tenant and user, then returns a controller configured with that user
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="scope">The service scope to use for dependency resolution</param>
  /// <param name="tenantName">Optional tenant name</param>
  /// <param name="userEmail">Optional user email</param>
  /// <param name="roles">Optional roles to assign to the user</param>
  /// <returns>A tuple containing the controller, tenant, and user</returns>
  public static async Task<(T controller, Tenant tenant, AppUser user)> CreateControllerWithTestData<T>(
    this IServiceScope scope,
    string tenantName = "Test Tenant",
    string userEmail = "test@example.com",
    params string[] roles) where T : ControllerBase
  {
    var services = scope.ServiceProvider;
    var tenant = await services.CreateTestTenant(tenantName);

    // If the test caller should NOT be a server administrator, ensure there is at least one
    // existing user so the next created user will not become the automatic server admin.
    if (!roles.Contains(RoleNames.ServerAdministrator))
    {
      // create a seed user in the tenant so our test user is not the very first user
      await services.CreateTestUser(tenant.Id, email: "seed@t.local");
    }

    var user = await services.CreateTestUser(tenant.Id, userEmail, roles);

    // Ensure the created controller user does not accidentally hold ServerAdministrator role
    // unless explicitly requested by the caller.
    if (!roles.Contains(RoleNames.ServerAdministrator))
    {
      var userManager = services.GetRequiredService<UserManager<AppUser>>();
      if (await userManager.IsInRoleAsync(user, RoleNames.ServerAdministrator))
      {
        await userManager.RemoveFromRoleAsync(user, RoleNames.ServerAdministrator);
      }
      // Also remove any persisted user-role links directly from the DB to ensure Identity
      // role lookups reflect the intended test state.
      var db = services.GetRequiredService<AppDb>();
      var userEntity = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == user.Id);
      if (userEntity is not null && userEntity.UserRoles?.Any() == true)
      {
        db.UserRoles.RemoveRange(userEntity.UserRoles);
        await db.SaveChangesAsync();
      }
    }
    var controller = await scope.CreateControllerWithUser<T>(user);

    return (controller, tenant, user);
  }

  /// <summary>
  /// Creates a controller configured with a test user context
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="scope">The service scope to use for dependency resolution</param>
  /// <param name="user">The user to configure for the controller</param>
  /// <returns>The configured controller instance</returns>
  public static async Task<T> CreateControllerWithUser<T>(this IServiceScope scope, AppUser user) where T : ControllerBase
  {
    var controller = scope.CreateController<T>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    await controller.SetControllerUser(user, userManager);

    return controller;
  }

  /// <summary>
  /// Creates a test device for the specified tenant and saves it to the database
  /// </summary>
  /// <param name="services"></param>
  /// <param name="tenantId">The tenant ID for the device</param>
  /// <param name="deviceId">Optional device ID, if not provided a new Guid will be used</param>
  /// <returns>The created Device entity</returns>
  public static async Task<Device> CreateTestDevice(
    this IServiceProvider services,
    Guid tenantId,
    Guid? deviceId = null)
  {
    using var scope = services.CreateScope();
    var deviceManager = scope.ServiceProvider.GetRequiredService<IDeviceManager>();
    var id = deviceId ?? Guid.NewGuid();
    var now = DateTimeOffset.UtcNow;
    var deviceDto = new DeviceDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: id,
      Is64Bit: true,
      IsOnline: true,
      LastSeen: now,
      OsArchitecture: System.Runtime.InteropServices.Architecture.X64,
      Platform: Libraries.Shared.Enums.SystemPlatform.Windows,
      ProcessorCount: 4,
      ConnectionId: "test-connection-id",
      OsDescription: "Windows 10",
      TenantId: tenantId,
      TotalMemory: 8192,
      TotalStorage: 256000,
      UsedMemory: 4096,
      UsedStorage: 128000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:11:22:33:44:55"],
      PublicIpV4: "127.0.0.1",
      PublicIpV6: "::1",
      LocalIpV4: "10.0.0.2",
      LocalIpV6: "fe80::2",
      Drives: [new Libraries.Shared.Models.Drive { Name = "C:", VolumeLabel = "System", TotalSize = 256000, FreeSpace = 128000 }]
    );
    var device = await deviceManager.AddOrUpdate(deviceDto);
    return device;
  }

  /// <summary>
  /// Creates a test tenant and saves it to the database
  /// </summary>
  /// <param name="services"></param>
  /// <param name="tenantName">Optional tenant name, defaults to "Test Tenant"</param>
  /// <returns>The created tenant</returns>
  public static async Task<Tenant> CreateTestTenant(this IServiceProvider services, string tenantName = "Test Tenant")
  {
    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var tenant = new Tenant { Id = Guid.NewGuid(), Name = tenantName };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    return tenant;
  }

  /// <summary>
  /// Creates a test user with the specified role and saves it to the database
  /// </summary>
  /// <param name="services"></param>
  /// <param name="tenantId">The tenant ID for the user</param>
  /// <param name="email">Optional email, defaults to "test@example.com"</param>
  /// <param name="roles">Optional roles to assign to the user</param>
  /// <returns>The created user</returns>
  public static async Task<AppUser> CreateTestUser(
    this IServiceProvider services,
    Guid tenantId,
    string email = "test@example.com",
    params string[] roles)
  {
    using var scope = services.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var userResult = await userCreator.CreateUser(email, "T3stP@ssw0rd!", tenantId);
    if (!userResult.Succeeded)
    {
      throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", userResult.IdentityResult.Errors.Select(e => e.Description))}");
    }

    var user = userResult.User;

    // Add roles if specified
    foreach (var role in roles)
    {
      // Skip if already in role to avoid duplicate-role errors in tests
      if (!await userManager.IsInRoleAsync(user, role))
      {
        var addResult = await userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
          throw new InvalidOperationException($"Failed to add role {role} to user: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
        }
      }
    }

    return user;
  }
  
  /// <summary>
  /// Creates a test user with the specified role and saves it to the database
  /// </summary>
  /// <param name="services"></param>
  /// <param name="email">Optional email, defaults to "test@example.com"</param>
  /// <param name="roles">Optional roles to assign to the user</param>
  /// <returns>The created user</returns>
  public static async Task<AppUser> CreateTestUser(
    this IServiceProvider services,
    string email = "test@example.com",
    params string[] roles)
  {
    using var scope = services.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var userResult = await userCreator.CreateUser(email, "T3stP@ssw0rd!", returnUrl: null);
    if (!userResult.Succeeded)
    {
      throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", userResult.IdentityResult.Errors.Select(e => e.Description))}");
    }

    var user = userResult.User;

    // Add roles if specified
    foreach (var role in roles)
    {
      // Skip if already in role to avoid duplicate-role errors in tests
      if (!await userManager.IsInRoleAsync(user, role))
      {
        var addResult = await userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
          throw new InvalidOperationException($"Failed to add role {role} to user: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
        }
      }
    }

    return user;
  }
}
