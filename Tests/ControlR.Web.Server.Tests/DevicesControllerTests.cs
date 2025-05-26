using System.Collections.Immutable;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class DevicesControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput; [Fact]
  public async Task GetDevicesGridData_AppliesCombinedFilters()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    var userCreator = testApp.App.Services.GetRequiredService<IUserCreator>();

    // Create test tenant
    var tenantId = Guid.NewGuid();
    var tenant = new Tenant { Id = tenantId, Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    // Create test user
    var userResult = await userCreator.CreateUser("test@example.com", "T3stP@ssw0rd!", tenantId);
    Assert.True(userResult.Succeeded);
    var user = userResult.User;

    var addResult = await userManager.AddToRoleAsync(user, RoleNames.DeviceSuperUser);
    Assert.True(addResult.Succeeded);

    // Create test tag
    var tagId = Guid.NewGuid();
    db.Tags.Add(new Tag { Id = tagId, Name = "TestTag", TenantId = tenantId });
    await db.SaveChangesAsync();

    // Create test devices with varying properties
    for (int i = 0; i < 10; i++)
    {
      var deviceId = Guid.NewGuid();
      var deviceDto = new DeviceDto(
          Name: $"Test Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: deviceId,
          Is64Bit: true,
          IsOnline: i % 2 == 0, // Even indexed devices are online
          LastSeen: DateTimeOffset.Now.AddMinutes(-i),
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          ConnectionId: $"test-connection-id-{i}",
          OsDescription: $"Windows {10 + i}",
          TenantId: tenantId,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192 + (i * 100),
          UsedStorage: 512000 + (i * 1000),
          CurrentUsers: [$"User{i}"],
          MacAddresses: [$"00:00:00:00:00:{i:D2}"],
          PublicIpV4: $"192.168.1.{i}",
          PublicIpV6: $"::1:{i}",
          Drives: [new Drive { Name = $"C{i}", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 - (i * 1000) }])
      {
        TagIds = i < 5 ? new[] { tagId }.ToImmutableArray() : null // First 5 devices have the tag
      };

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: true);
    }    // Configure controller user context for authorization
    await controller.SetControllerUser(user, userManager);

    // Act - Combined filters: online + has tag + contains "Device 2" in name
    var request = new DeviceSearchRequestDto
    {
      HideOfflineDevices = true,
      TagIds = [tagId],
      SearchText = "device 2",
      Page = 0,
      PageSize = 10
    };

    var result = await controller.SearchDevices(
        request,
        db,
        testApp.App.Services.GetRequiredService<IAuthorizationService>(),
        testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());

    var response = result.Value;

    // Assert
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    Assert.Single(response.Items); // Should only match "Test Device 2" which is online and has the tag
    var device = response.Items[0]; Assert.Equal("Test Device 2", device.Name);
    Assert.True(device.IsOnline);
    Assert.NotNull(device.TagIds);
    Assert.Contains(tagId, device.TagIds!);
  }
  [Fact]
  public async Task GetDevicesGridData_RespectsUserAuthorization()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    var userCreator = testApp.App.Services.GetRequiredService<IUserCreator>();

    // Create two tenants
    var tenant1Id = Guid.NewGuid();
    var tenant1 = new Tenant { Id = tenant1Id, Name = "Tenant 1" };
    db.Tenants.Add(tenant1);

    var tenant2Id = Guid.NewGuid();
    var tenant2 = new Tenant { Id = tenant2Id, Name = "Tenant 2" };
    db.Tenants.Add(tenant2);
    await db.SaveChangesAsync();

    // Create user for tenant 1
    var userResult = await userCreator.CreateUser("user1@example.com", "T3stP@ssw0rd!", tenant1Id);
    Assert.True(userResult.Succeeded);
    var user1 = userResult.User;

    var addResult = await userManager.AddToRoleAsync(user1, RoleNames.DeviceSuperUser);
    Assert.True(addResult.Succeeded);

    // Create devices for both tenants
    for (int i = 0; i < 5; i++)
    {
      // Tenant 1 device
      var device1Id = Guid.NewGuid();
      var device1Dto = new DeviceDto(
          Name: $"Tenant1 Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: device1Id,
          Is64Bit: true,
          IsOnline: true,
          LastSeen: DateTimeOffset.Now,
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          ConnectionId: $"tenant1-connection-{i}",
          OsDescription: "Windows 10",
          TenantId: tenant1Id,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192,
          UsedStorage: 512000,
          CurrentUsers: ["User1"],
          MacAddresses: ["00:00:00:00:00:01"],
          PublicIpV4: "192.168.1.1",
          PublicIpV6: "::1",
          Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      await deviceManager.AddOrUpdate(device1Dto, addTagIds: false);

      // Tenant 2 device
      var device2Id = Guid.NewGuid();
      var device2Dto = new DeviceDto(
          Name: $"Tenant2 Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: device2Id,
          Is64Bit: true,
          IsOnline: true,
          LastSeen: DateTimeOffset.Now,
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          ConnectionId: $"tenant2-connection-{i}",
          OsDescription: "Windows 10",
          TenantId: tenant2Id,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192,
          UsedStorage: 512000,
          CurrentUsers: ["User2"],
          MacAddresses: ["00:00:00:00:00:02"],
          PublicIpV4: "192.168.1.2",
          PublicIpV6: "::2",
          Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      await deviceManager.AddOrUpdate(device2Dto, addTagIds: false);
    }

    // Configure controller user context for authorization
    await controller.SetControllerUser(user1, userManager);

    // Act
    var request = new DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 20 // Request all devices
    };

    var result = await controller.SearchDevices(
        request,
        db,
        testApp.App.Services.GetRequiredService<IAuthorizationService>(),
        testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());
    var response = result.Value;

    // Assert
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    Assert.Equal(5, response.Items.Count); // Should only see tenant 1's devices
    Assert.All(response.Items, device => Assert.Equal(tenant1Id, device.TenantId));
    Assert.All(response.Items, device => Assert.StartsWith("Tenant1", device.Name));
  }

  [Fact]
  public async Task GetDevicesGridData_ReturnsCorrectDevices()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = new TestDevicesController();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    var userCreator = testApp.App.Services.GetRequiredService<IUserCreator>();

    // Create test tenant
    var tenantId = Guid.NewGuid();
    var tenant = new Tenant { Id = tenantId, Name = "Test Tenant" };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    // Create test user
    var userResult = await userCreator.CreateUser("test@example.com", "T3stP@ssw0rd!", tenantId);
    Assert.True(userResult.Succeeded);

    var user = userResult.User;

    var addResult = await userManager.AddToRoleAsync(user, RoleNames.DeviceSuperUser);
    Assert.True(addResult.Succeeded);

    // Create test tags
    var tagIds = new Guid[] { Guid.NewGuid(), Guid.NewGuid() }.ToImmutableArray();
    foreach (var tagId in tagIds)
    {
      db.Tags.Add(new Tag { Id = tagId, Name = $"Tag {tagId}", TenantId = tenantId });
    }
    await db.SaveChangesAsync();

    // Create test devices
    for (int i = 0; i < 10; i++)
    {
      var deviceId = Guid.NewGuid();
      var deviceDto = new DeviceDto(
          Name: $"Test Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: deviceId,
          Is64Bit: true,
          IsOnline: i % 2 == 0, // Even indexed devices are online
          LastSeen: DateTimeOffset.Now.AddMinutes(-i),
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          ConnectionId: $"test-connection-id-{i}",
          OsDescription: $"Windows {10 + i}",
          TenantId: tenantId,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192 + (i * 100),
          UsedStorage: 512000 + (i * 1000),
          CurrentUsers: [$"User{i}"],
          MacAddresses: [$"00:00:00:00:00:{i:D2}"],
          PublicIpV4: $"192.168.1.{i}",
          PublicIpV6: $"::1:{i}",
          Drives: [new Drive { Name = $"C{i}", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 - (i * 1000) }])
      {
        TagIds = i % 3 == 0 ? tagIds : null // Assign tags to every third device
      };

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: true);
    }        // Configure controller user context for authorization
    await controller.SetControllerUser(user, userManager);

    // Act
    // Test case 1: Get all devices with pagination
    var request1 = new DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 5
    }; var result1 = await controller.GetDevicesGridData(
        request1,
        db,
        testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());
    var response1 = result1.Value;

    // Test case 2: Filter by online status
    var request2 = new DeviceSearchRequestDto
    {
      HideOfflineDevices = true,
      Page = 0,
      PageSize = 10
    }; var result2 = await controller.GetDevicesGridData(
        request2,
        db,
        testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());
    var response2 = result2.Value;

    // Test case 3: Filter by tag
    var request3 = new DeviceSearchRequestDto
    {
      TagIds = [tagIds[0]],
      Page = 0,
      PageSize = 10
    };
    var result3 = await controller.GetDevicesGridData(
        request3,
        db,
        testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());
    var response3 = result3.Value;
    // Test case 4: Search by name
    var request4 = new DeviceSearchRequestDto
    {
      SearchText = "Device 1",
      Page = 0,
      PageSize = 10
    };
    var result4 = await controller.GetDevicesGridData(
        request4,
        db,
        testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());
    var response4 = result4.Value;

    // Test case 5: Sort by CPU utilization (descending)
    var request5 = new DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 10,
      SortDefinitions = [new DeviceColumnSort { PropertyName = "CpuUtilization", Descending = true, SortOrder = 0 }]
    };
    var result5 = await controller.GetDevicesGridData(
        request5,
        db,
        testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());
    var response5 = result5.Value;

    // Assert
    // Test case 1: Pagination
    Assert.NotNull(response1);
    Assert.NotNull(response1.Items);
    Assert.Equal(5, response1.Items.Count);
    Assert.Equal(10, response1.TotalItems);

    // Test case 2: Filter by online status
    Assert.NotNull(response2);
    Assert.NotNull(response2.Items);
    Assert.All(response2.Items, device => Assert.True(device.IsOnline));
    Assert.Equal(5, response2.Items.Count); // Half of the devices are online        // Test case 3: Filter by tag
    Assert.NotNull(response3);
    Assert.NotNull(response3.Items);
    Assert.All(response3.Items, device => Assert.NotNull(device.TagIds));
    Assert.All(response3.Items, device => Assert.Contains(tagIds[0], device.TagIds!));

    // Test case 4: Search by name
    Assert.NotNull(response4);
    Assert.NotNull(response4.Items);
    Assert.All(response4.Items, device => Assert.Contains("Device 1", device.Name));

    // Test case 5: Sort by CPU utilization (descending)
    Assert.NotNull(response5);
    Assert.NotNull(response5.Items);
    for (int i = 0; i < response5.Items.Count - 1; i++)
    {
      Assert.True(response5.Items[i].CpuUtilization >= response5.Items[i + 1].CpuUtilization);
    }
  }
}
