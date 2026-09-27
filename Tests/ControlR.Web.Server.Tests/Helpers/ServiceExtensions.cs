using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.PermissionAssignments;
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
    var (principal, _, _) = await TestPrincipalHelper.CreateServerServiceAccountAsync(scope.ServiceProvider, accountName);
    var controller = scope.CreateController<T>();
    controller.ControllerContext.HttpContext.User = principal;
    return controller;
  }

  /// <summary>
  /// Creates a test tenant and user, then returns a controller configured with that user
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="scope">The service scope to use for dependency resolution</param>
  /// <param name="tenantName">Optional tenant name</param>
  /// <param name="userEmail">Optional user email</param>
  /// <param name="presets">Optional permission presets to assign to the user</param>
  /// <returns>A tuple containing the controller, tenant, and user</returns>
  public static async Task<(T controller, Tenant tenant, AppUser user)> CreateControllerWithTestData<T>(
    this IServiceScope scope,
    string tenantName = "Test Tenant",
    string userEmail = "test@example.com",
    params string[] presets) where T : ControllerBase
  {
    var services = scope.ServiceProvider;
    var tenant = await services.CreateTestTenant(tenantName);

    // Ensure there is a seed user so our test user won't become the first-user admin automatically.
    if (!presets.Contains(PermissionPresets.ServerAdministrator))
    {
      await services.CreateTestUser(tenant.Id, email: "seed@t.local");
    }

    var user = await services.CreateTestUser(tenant.Id, userEmail, presets);
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
    var accountResult = await manager.CreateForServer(accountNameValue, null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);

    Assert.True(accountResult.IsSuccess);

    var credResult = await manager.AddCredentialForServer(
      accountResult.Value.Id, "Credential", expiresAt: null, TestActors.ServerServiceAccount(accountResult.Value.Id), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    return TestPrincipalHelper.CreateServerServiceAccountPrincipal(accountResult.Value, credResult.Value.Credential);
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
    Guid? deviceId = null,
    string? publicKeyBase64 = null)
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

    var device = await deviceManager.AddOrUpdate(deviceDto, connectionContext, tagIds: null, publicKeyBase64: publicKeyBase64);
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
  /// Creates a test user with the specified permission presets and saves it to the database.
  /// </summary>
  /// <param name="services">The service provider.</param>
  /// <param name="tenantId">The tenant ID for the user.</param>
  /// <param name="email">Optional email, defaults to "test@example.com".</param>
  /// <param name="presets">Optional permission presets to assign to the user.</param>
  /// <returns>The created user.</returns>
  public static async Task<AppUser> CreateTestUser(
    this IServiceProvider services,
    Guid tenantId,
    string email = "test@example.com",
    params string[] presets)
  {
    using var scope = services.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

    var userResult = await userCreator.CreateUser(email, "T3stP@ssw0rd!", tenantId);
    if (!userResult.Succeeded)
    {
      throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", userResult.IdentityResult.Errors.Select(e => e.Description))}");
    }

    var user = userResult.User;
    await SeedPresetAssignmentsAsync(scope.ServiceProvider, user, presets);
    return user;
  }

  /// <summary>
  /// Creates a test user in a brand-new, isolated tenant (no tenantId supplied), and saves it
  /// to the database.
  /// </summary>
  /// <remarks>
  /// Because no tenant is supplied, <see cref="IUserCreator"/> creates a new isolated tenant for
  /// this user. The user also becomes the self-registered first-user server administrator when
  /// the app instance is empty (as it is for a fresh <c>TestAppBuilder.CreateTestApp</c>), and
  /// NOT when other users already exist. This mirrors production's first-user-self-registration
  /// behavior. Prefer passing an explicit <c>tenantId</c> (and an explicit
  /// <c>PermissionPresets.ServerAdministrator</c> when a server admin is intended) to avoid
  /// depending on this value.
  /// </remarks>
  /// <param name="services">The service provider.</param>
  /// <param name="email">Optional email, defaults to "test@example.com".</param>
  /// <param name="presets">Optional permission presets to assign to the user.</param>
  /// <returns>The created user.</returns>
  public static async Task<AppUser> CreateTestUser(
    this IServiceProvider services,
    string email = "test@example.com",
    params string[] presets)
  {
    using var scope = services.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

    var userResult = await userCreator.CreateUser(email, "T3stP@ssw0rd!", returnUrl: null);
    if (!userResult.Succeeded)
    {
      throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", userResult.IdentityResult.Errors.Select(e => e.Description))}");
    }

    var user = userResult.User;
    await SeedPresetAssignmentsAsync(scope.ServiceProvider, user, presets);
    return user;
  }

  private static async Task SeedPresetAssignmentsAsync(
    IServiceProvider provider,
    AppUser user,
    IEnumerable<string> presetNames)
  {
    var seeder = provider.GetRequiredService<IPermissionAssignmentSeeder>();
    await seeder.SeedAssignments(user.Id, user.TenantId, presetNames);
  }
}
