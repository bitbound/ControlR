using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Web.Server.Services.DeviceManagement;
using System.Net;
using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;

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
  /// Creates a controller with a server principal already configured.
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="scope">The service scope to use for dependency resolution</param>
  /// <param name="accountName">Optional name for the server service account</param>
  /// <returns>The configured controller instance</returns>
  public static async Task<T> CreateControllerWithServerPrincipal<T>(this IServiceScope scope, string? accountName = null) where T : ControllerBase
  {
    var serverPrincipal = await scope.ServiceProvider.CreateServerPrincipal(accountName);
    var controller = scope.CreateController<T>();
    controller.ControllerContext.HttpContext.User = serverPrincipal;
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

    // Ensure there is a seed user so our test user won't become the first-user admin automatically.
    if (!roles.Contains(RoleNames.ServerAdministrator))
    {
      await services.CreateTestUser(tenant.Id, email: "seed@t.local");
    }

    var user = await services.CreateTestUser(tenant.Id, userEmail, roles);
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
  /// Creates a server principal <see cref="ClaimsPrincipal"/> by creating a new server-scoped
  /// service account and returning a principal with the appropriate claims.
  /// </summary>
  public static async Task<ClaimsPrincipal> CreateServerPrincipal(this IServiceProvider services, string? accountName = null)
  {
    var manager = services.GetRequiredService<IServiceAccountManager>();
    var accountNameValue = accountName ?? $"server-principal-{Guid.NewGuid():N}";
    var result = await manager.CreateForServer(accountNameValue, null, TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    return TestPrincipalHelper.CreateServerServiceAccountPrincipal(result.Value);
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
    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: id,
      Is64Bit: true,
      OsArchitecture: System.Runtime.InteropServices.Architecture.X64,
      Platform: Libraries.Api.Contracts.Enums.SystemPlatform.Windows,
      ProcessorCount: 4,
      OsDescription: "Windows 10",
      TenantId: tenantId,
      TotalMemory: 8192,
      TotalStorage: 256000,
      UsedMemory: 4096,
      UsedStorage: 128000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:11:22:33:44:55"],
      LocalIpV4: "10.0.0.2",
      LocalIpV6: "fe80::2",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 256000, FreeSpace = 128000 }]
    );

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-connection-id",
      RemoteIpAddress: IPAddress.Loopback,
      LastSeen: now,
      IsOnline: true
    );

    var device = await deviceManager.AddOrUpdate(deviceDto, connectionContext, tagIds: null);
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
  /// Creates a test user with the specified roles and saves it to the database.
  /// </summary>
  /// <param name="services">The service provider.</param>
  /// <param name="tenantId">The tenant ID for the user.</param>
  /// <param name="email">Optional email, defaults to "test@example.com".</param>
  /// <param name="roles">Optional roles to assign to the user.</param>
  /// <returns>The created user.</returns>
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
    await AddRolesIfMissingAsync(userManager, user, roles);
    return user;
  }

  /// <summary>
  /// Creates a test user with the specified roles and saves it to the database.
  /// </summary>
  /// <param name="services">The service provider.</param>
  /// <param name="email">Optional email, defaults to "test@example.com".</param>
  /// <param name="roles">Optional roles to assign to the user.</param>
  /// <returns>The created user.</returns>
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
    await AddRolesIfMissingAsync(userManager, user, roles);
    return user;
  }

  private static async Task AddRolesIfMissingAsync(UserManager<AppUser> userManager, AppUser user, IEnumerable<string> roles)
  {
    var existingRoles = new HashSet<string>(await userManager.GetRolesAsync(user));
    foreach (var role in roles)
    {
      if (!existingRoles.Contains(role))
      {
        var addResult = await userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
          throw new InvalidOperationException($"Failed to add role {role} to user: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
        }
        existingRoles.Add(role);
      }
    }
  }
}
