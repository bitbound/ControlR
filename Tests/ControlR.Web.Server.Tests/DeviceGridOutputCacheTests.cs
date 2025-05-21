using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Middleware;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    var controller = testApp.CreateController<ControlR.Web.Server.Api.DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();
    var outputCacheStore = testApp.App.Services.GetRequiredService<IOutputCacheStore>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();

    // Create test tenant
    var tenantId = Guid.NewGuid();
    var tenant = new Tenant { Id = tenantId, Name = "Output Cache Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    // Create test user
    var userId = Guid.NewGuid();
    var user = new AppUser
    {
      Id = userId,
      UserName = "cachetest@example.com",
      Email = "cachetest@example.com",
      TenantId = tenantId
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    // Create test device
    var deviceId = Guid.NewGuid();
    var deviceDto = new DeviceDto(
        Name: "Cache Test Device",
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: deviceId,
        Is64Bit: true,
        IsOnline: true,
        LastSeen: DateTimeOffset.Now,
        OsArchitecture: System.Runtime.InteropServices.Architecture.X64,
        Platform: ControlR.Libraries.Shared.Enums.SystemPlatform.Windows,
        ProcessorCount: 8,
        ConnectionId: "test-connection-id",
        OsDescription: "Windows 11",
        TenantId: tenantId,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["TestUser"],
        MacAddresses: ["00:00:00:00:00:01"],
        PublicIpV4: "192.168.1.1",
        PublicIpV6: "::1:1",
        Drives: [new ControlR.Libraries.Shared.Models.Drive { Name = "C", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]); await deviceManager.AddOrUpdate(deviceDto, addTagIds: true);

    // Configure controller user context for authorization
    await testApp.SetControllerUser(controller, user);

    // Create the request
    var request = new DeviceGridRequestDto
    {
      Page = 0,
      PageSize = 10
    };

    // Act - First call should hit the database
    var result1 = await controller.GetDevicesGridData(
        request,
        db,
        testApp.App.Services.GetRequiredService<IAuthorizationService>(),
        testApp.App.Services.GetRequiredService<ILogger<ControlR.Web.Server.Api.DevicesController>>());

    // Create new device to test cache invalidation
    var newDeviceId = Guid.NewGuid();
    var newDeviceDto = deviceDto with { Id = newDeviceId, Name = "New Cache Test Device" };
    await deviceManager.AddOrUpdate(newDeviceDto, addTagIds: true);

    // Simulate cache invalidation
    await outputCacheStore.EvictByTagAsync("device-grid", default);

    // Act - Third call after invalidation should get updated data
    var result3 = await controller.GetDevicesGridData(request, db, testApp.App.Services.GetRequiredService<IAuthorizationService>(), testApp.App.Services.GetRequiredService<ILogger<ControlR.Web.Server.Api.DevicesController>>());

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
    context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
    {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
    }, "TestAuth"));

    context.EnableOutputCaching = false;

    // Act - Authenticated user
    await policy.CacheRequestAsync(context, default);
    var authenticatedResult = context.EnableOutputCaching;

    // Assert
    Assert.False(unauthenticatedResult);
    Assert.True(authenticatedResult);
  }
}
