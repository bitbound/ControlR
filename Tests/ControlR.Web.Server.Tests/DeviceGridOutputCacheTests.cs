using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Middleware;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class DeviceGridOutputCacheTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task GetDevicesGridData_WithOutputCache_ReturnsAndCachesData()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.Services.CreateScope();
    var controller = scope.CreateController<Api.DevicesController>();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var outputCacheStore = scope.ServiceProvider.GetRequiredService<IOutputCacheStore>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
    var deviceManager = scope.ServiceProvider.GetRequiredService<IDeviceManager>();

    // Create test user
    var userResult = await userCreator.CreateUser("cachetest@example.com", "T3stP@ssw0rd!",returnUrl: null);
    Assert.True(userResult.Succeeded);

    var user = userResult.User;

    // Create test device
    var deviceId = Guid.NewGuid();
    var deviceDto = new DeviceUpdateRequestDto(
        Name: "Cache Test Device",
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: deviceId,
        Is64Bit: true,
        OsArchitecture: System.Runtime.InteropServices.Architecture.X64,
        Platform: Libraries.Shared.Enums.SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: user.TenantId,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["TestUser"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: "192.168.0.1",
        LocalIpV6: "fe80::1",
        Drives: []);

    var connectionContext = new DeviceConnectionContext(
        ConnectionId: "test-connection-id",
        RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: true);

    await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    await db.SaveChangesAsync(); // Ensure the device is saved to the database

    // Force a database refresh to ensure entity is tracked
    var device = db.Devices.Find(deviceId);
    if (device != null)
    {
      await db.Entry(device).ReloadAsync();
    }

    // Configure controller user context for authorization
    await controller.SetControllerUser(user, userManager);

    // Create the request
    var request = new DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 10
    };

    // Act - First call should hit the database
    var result1 = await controller.SearchDevices(
        request,
        db,
        scope.ServiceProvider.GetRequiredService<IAuthorizationService>(),
        scope.ServiceProvider.GetRequiredService<IAgentVersionProvider>(),
        scope.ServiceProvider.GetRequiredService<ILogger<Api.DevicesController>>());

    // Create new device to test cache invalidation
    var newDeviceId = Guid.NewGuid();
    var newDeviceDto = new DeviceUpdateRequestDto(
        Name: "New Cache Test Device",
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: newDeviceId,
        Is64Bit: true,
        OsArchitecture: System.Runtime.InteropServices.Architecture.X64,
        Platform: Libraries.Shared.Enums.SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: user.TenantId,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["TestUser"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: "192.168.0.1",
        LocalIpV6: "fe80::1",
        Drives: []);

    var newConnectionContext = new DeviceConnectionContext(
        ConnectionId: "test-connection-id-2",
        RemoteIpAddress: IPAddress.Parse("192.168.1.2"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: true);

    await deviceManager.AddOrUpdate(newDeviceDto, newConnectionContext);

    // Simulate cache invalidation
    await outputCacheStore.EvictByTagAsync("device-grid", CancellationToken.None);

    // Act - Third call after invalidation should get updated data
    var result3 = await controller.SearchDevices(
      request,
      db,
      scope.ServiceProvider.GetRequiredService<IAuthorizationService>(),
      scope.ServiceProvider.GetRequiredService<IAgentVersionProvider>(),
      scope.ServiceProvider.GetRequiredService<ILogger<Api.DevicesController>>());

    // Assert
    Assert.NotNull(result1.Value);
    Assert.NotNull(result1.Value.Items);
    Assert.Single(result1.Value.Items); // First call should have one device

    Assert.NotNull(result3.Value);
    Assert.NotNull(result3.Value.Items);
    Assert.Equal(2, result3.Value.Items.Count); // After cache invalidation we should have two devices
    Assert.Contains(result3.Value.Items, d => d.Id == newDeviceId);
  }

  [Fact]
  public async Task OutputCachePolicy_OnlyEnablesCachingForAuthenticatedUsers()
  {
    // Arrange
    var policy = new DeviceGridOutputCachePolicy();
    var context = new OutputCacheContext
    {
      HttpContext = new DefaultHttpContext(),
      EnableOutputCaching = false
    };

    // Act - Unauthenticated user
    await policy.CacheRequestAsync(context, default);
    var unauthenticatedResult = context.EnableOutputCaching;

    // Set authenticated user
    context.HttpContext.User = new ClaimsPrincipal(
      new ClaimsIdentity(
      [
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
      ],
      "TestAuth"));

    context.EnableOutputCaching = false;

    // Act - Authenticated user
    await policy.CacheRequestAsync(context, default);
    var authenticatedResult = context.EnableOutputCaching;

    // Assert
    Assert.False(unauthenticatedResult);
    Assert.True(authenticatedResult);
  }
}
