using System.Collections.Immutable;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using ControlR.Web.Server.Api.Internal;

namespace ControlR.Web.Server.Tests;

public class DevicesControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task DeleteMany_NonExistentDevicesReturnedInFailureIds()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // One device exists, two don't
    var existingId = Guid.NewGuid();
    var nonExistentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Existing Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: existingId,
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 4,
      OsDescription: "Windows 10",
      TenantId: tenant.Id,
      TotalMemory: 8192,
      TotalStorage: 256000,
      UsedMemory: 4096,
      UsedStorage: 128000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:11:22:33:44:55"],
      LocalIpV4: "10.0.0.2",
      LocalIpV6: "fe80::2",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 256000, FreeSpace = 128000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-conn",
      RemoteIpAddress: IPAddress.Loopback,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: true);

    await deviceManager.AddOrUpdate(deviceDto, connectionContext);

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());

    // Act
    var request = new InternalDtos.DeleteDevicesRequestDto([.. nonExistentIds, existingId]);
    var result = await controller.DeleteMany(db, request, TestContext.Current.CancellationToken);

    // Assert
    var response = result.Value;
    Assert.NotNull(response);
    Assert.Single(response.SuccessIds);
    Assert.Contains(existingId, response.SuccessIds);
    Assert.Equal(nonExistentIds.Length, response.FailureIds.Count);
    Assert.All(nonExistentIds, id => Assert.Contains(id, response.FailureIds));
  }

  [Fact]
  public async Task DeleteMany_PartialDelete_ReturnsMixedSuccessAndFailure()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create an abundance of devices (15 total).
    // 10 will be requested for deletion, 5 stay untouched.
    var allDeviceIds = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToList();
    var deleteRequestIds = allDeviceIds.Take(10).ToList();
    var unaffectedIds = allDeviceIds.Skip(10).ToList();

    // 3 non-existent IDs → "not found" failures
    var nonExistentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

    // 3 IDs that the trigger will skip → "failed to delete"
    var triggerSkipIds = deleteRequestIds.Take(3).ToList();

    var deviceManager = services.GetRequiredService<IDeviceManager>();

    foreach (var id in allDeviceIds)
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Device {id}",
        AgentVersion: "1.0.0",
        CpuUtilization: 10,
        Id: id,
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 4,
        OsDescription: "Windows 10",
        TenantId: tenant.Id,
        TotalMemory: 8192,
        TotalStorage: 256000,
        UsedMemory: 4096,
        UsedStorage: 128000,
        CurrentUsers: ["TestUser"],
        MacAddresses: ["00:11:22:33:44:55"],
        LocalIpV4: "10.0.0.2",
        LocalIpV6: "fe80::2",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 256000, FreeSpace = 128000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: "test-conn",
        RemoteIpAddress: IPAddress.Loopback,
        LastSeen: DateTimeOffset.UtcNow,
        IsOnline: true);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    foreach (var id in allDeviceIds)
    {
      Assert.True(await db.Devices.AnyAsync(x => x.Id == id, TestContext.Current.CancellationToken));
    }

    // Install a BEFORE DELETE trigger that silently suppresses deletion of
    // 3 specific device IDs. ExecuteDelete will skip those rows, returning
    // deletedCount < authorizedDeviceIds.Count, and they survive to appear
    // in remainingIds → failureIds as "failed to delete".
    var skipIdLiterals = string.Join(", ", triggerSkipIds.Select(id => $"'{id}'::uuid"));
    var createTriggerSql =
      $"""
      CREATE OR REPLACE FUNCTION _test_skip_device_delete() RETURNS trigger AS $$
      BEGIN
        IF OLD."Id" IN ({skipIdLiterals}) THEN
          RETURN NULL;
        END IF;
        RETURN OLD;
      END;
      $$ LANGUAGE plpgsql;

      CREATE TRIGGER _test_skip_device_delete
      BEFORE DELETE ON "Devices"
      FOR EACH ROW EXECUTE FUNCTION _test_skip_device_delete();
      """;

    await db.Database.ExecuteSqlRawAsync(createTriggerSql, TestContext.Current.CancellationToken);

    try
    {
      await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());

      // Act — request 10 existing + 3 non-existent
      var request = new InternalDtos.DeleteDevicesRequestDto([.. deleteRequestIds, .. nonExistentIds]);
      var result = await controller.DeleteMany(db, request, TestContext.Current.CancellationToken);

      // Assert
      var response = result.Value;
      Assert.NotNull(response);

      // 7 devices successfully deleted (10 requested - 3 skipped by trigger)
      Assert.Equal(7, response.SuccessIds.Count);
      foreach (var id in deleteRequestIds.Except(triggerSkipIds))
      {
        Assert.Contains(id, response.SuccessIds);
      }

      // 6 failures: 3 trigger-skipped ("failed to delete") + 3 non-existent ("not found")
      Assert.Equal(6, response.FailureIds.Count);
      foreach (var id in triggerSkipIds)
      {
        Assert.Contains(id, response.FailureIds);
      }
      foreach (var id in nonExistentIds)
      {
        Assert.Contains(id, response.FailureIds);
      }

      // Unaffected devices should still exist
      foreach (var id in unaffectedIds)
      {
        Assert.True(await db.Devices.AnyAsync(x => x.Id == id, TestContext.Current.CancellationToken));
      }
    }
    finally
    {
      // Clean up the trigger and function
      await db.Database.ExecuteSqlRawAsync(
        """
        DROP TRIGGER IF EXISTS _test_skip_device_delete ON "Devices";
        DROP FUNCTION IF EXISTS _test_skip_device_delete();
        """,
        TestContext.Current.CancellationToken);
    }
  }

  [Fact]
  public async Task DeleteMany_ReturnsBadRequestWhenNoTenantId()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var controller = scope.CreateController<DevicesController>();

    var request = new InternalDtos.DeleteDevicesRequestDto([Guid.NewGuid()]);

    // Act
    var result = await controller.DeleteMany(
      scope.ServiceProvider.GetRequiredService<AppDb>(),
      request,
      TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task DeleteMany_SpecifiedDevicesDeleted_UnaffectedDevicesRemain()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, useInMemoryDatabase: false);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Devices that exist and will be requested for deletion
    var deleteIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
    // Devices that exist but are NOT requested (should remain unaffected)
    var unaffectedIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

    var deviceManager = services.GetRequiredService<IDeviceManager>();

    // Create all existing devices
    foreach (var id in deleteIds.Concat(unaffectedIds))
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Device {id}",
        AgentVersion: "1.0.0",
        CpuUtilization: 10,
        Id: id,
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 4,
        OsDescription: "Windows 10",
        TenantId: tenant.Id,
        TotalMemory: 8192,
        TotalStorage: 256000,
        UsedMemory: 4096,
        UsedStorage: 128000,
        CurrentUsers: ["TestUser"],
        MacAddresses: ["00:11:22:33:44:55"],
        LocalIpV4: "10.0.0.2",
        LocalIpV6: "fe80::2",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 256000, FreeSpace = 128000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: "test-conn",
        RemoteIpAddress: IPAddress.Loopback,
        LastSeen: DateTimeOffset.UtcNow,
        IsOnline: true);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    // Verify devices exist before deletion
    foreach (var id in deleteIds.Concat(unaffectedIds))
    {
      Assert.True(await db.Devices.AnyAsync(x => x.Id == id, TestContext.Current.CancellationToken));
    }

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());

    // Act
    var request = new InternalDtos.DeleteDevicesRequestDto(deleteIds);
    var result = await controller.DeleteMany(db, request, TestContext.Current.CancellationToken);

    // Assert
    var response = result.Value;
    Assert.NotNull(response);
    Assert.Equal(deleteIds.Length, response.SuccessIds.Count);
    Assert.All(deleteIds, id => Assert.Contains(id, response.SuccessIds));
    Assert.Empty(response.FailureIds);

    // Unspecified devices should still exist
    foreach (var id in unaffectedIds)
    {
      Assert.True(await db.Devices.AnyAsync(x => x.Id == id, TestContext.Current.CancellationToken));
    }
  }

  [Fact]
  public async Task GetDevicesGridData_AppliesCombinedFilters()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    // Create test tenant and user
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create test tag
    var tagId = Guid.NewGuid();
    db.Tags.Add(new Tag { Id = tagId, Name = "TestTag", TenantId = tenant.Id });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Create test devices with varying properties
    for (int i = 0; i < 10; i++)
    {
      var deviceId = Guid.NewGuid();
      var deviceDto = new DeviceUpdateRequestDto(
          Name: $"Test Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: deviceId,
          Is64Bit: true,
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          OsDescription: $"Windows {10 + i}",
          TenantId: tenant.Id,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192 + (i * 100),
          UsedStorage: 512000 + (i * 1000),
          CurrentUsers: [$"User{i}"],
          MacAddresses: [$"00:00:00:00:00:{i:D2}"],
          LocalIpV4: $"192.168.0.{i}",
          LocalIpV6: $"fe80::{i}",
          Drives: [new Drive { Name = $"C{i}", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 - (i * 1000) }]);

      var connectionContext = new DeviceConnectionContext(
          ConnectionId: $"test-connection-id-{i}",
          RemoteIpAddress: IPAddress.Parse($"192.168.1.{i}"),
          LastSeen: DateTimeOffset.Now.AddMinutes(-i),
          IsOnline: i % 2 == 0);

      var tagIds = i < 5 ? new[] { tagId } : null;
      await deviceManager.AddOrUpdate(deviceDto, connectionContext, tagIds);
    }

    // Configure controller user context for authorization
    await controller.SetControllerUser(user, userManager);

    // Act - Combined filters: online + has tag + contains "Device 2" in name
    var request = new InternalDtos.DeviceSearchRequestDto
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
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    var response = result.Value;

    // Assert
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    Assert.Single(response.Items);
    var device = response.Items[0]; Assert.Equal("Test Device 2", device.Name);
    Assert.True(device.IsOnline);
    Assert.NotNull(device.TagIds);
    Assert.Contains(tagId, device.TagIds!);
  }

  [Fact]
  public async Task GetDevicesGridData_FilterByBooleanProperties()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with different online status
    for (var i = 0; i < 5; i++)
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: $"192.168.0.{i}",
        LocalIpV6: $"fe80::{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"conn-{i}",
        RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: i % 2 == 0);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    await controller.SetControllerUser(user, userManager);
    var onlineRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "true" }],
      Page = 0,
      PageSize = 10
    };

    var onlineResult = await controller.SearchDevices(
      onlineRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    var offlineRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "false" }],
      Page = 0,
      PageSize = 10
    };

    var offlineResult = await controller.SearchDevices(
      offlineRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    Assert.NotNull(onlineResult.Value);
    Assert.NotNull(onlineResult.Value.Items);
    Assert.Equal(3, onlineResult.Value.Items.Count);
    Assert.All(onlineResult.Value.Items, device => Assert.True(device.IsOnline));

    Assert.NotNull(offlineResult.Value);
    Assert.NotNull(offlineResult.Value.Items);
    Assert.Equal(2, offlineResult.Value.Items.Count);
    Assert.All(offlineResult.Value.Items, device => Assert.False(device.IsOnline));
  }

  [Fact]
  public async Task GetDevicesGridData_FilterByMultipleColumns()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with specific combinations for multi-filter testing
    var testData = new[]
    {
      new { Name = "Production-Web-01", IsOnline = true, CpuUtilization = 0.8, OsDescription = "Windows Server 2022" },
      new { Name = "Production-Web-02", IsOnline = true, CpuUtilization = 0.2, OsDescription = "Windows Server 2022" },
      new { Name = "Development-Web-01", IsOnline = false, CpuUtilization = 0.9, OsDescription = "Windows 11" },
      new { Name = "Production-DB-01", IsOnline = true, CpuUtilization = 0.9, OsDescription = "Windows Server 2019" },
      new { Name = "Test-Server-01", IsOnline = false, CpuUtilization = 0.1, OsDescription = "Ubuntu 22.04" }
    };

    for (int i = 0; i < testData.Length; i++)
    {
      var data = testData[i];
      var deviceDto = new DeviceUpdateRequestDto(
        Name: data.Name,
        AgentVersion: "1.0.0",
        CpuUtilization: data.CpuUtilization,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: data.OsDescription,
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: $"192.168.0.{i}",
        LocalIpV6: $"fe80::{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"conn-{i}",
        RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: data.IsOnline);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    await controller.SetControllerUser(user, userManager);

    // Test multiple filters: Online + High CPU + Contains "Production"
    var multiFilterRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [
        new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "true" },
        new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.GreaterThan, Value = "0.5" },
        new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.Contains, Value = "Production" }
      ],
      Page = 0,
      PageSize = 10
    };

    var multiFilterResult = await controller.SearchDevices(
      multiFilterRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Test OS + Online filters
    var osOnlineRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [
        new DeviceColumnFilter { PropertyName = "OsDescription", Operator = FilterOperator.String.Contains, Value = "Windows Server" },
        new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "true" }
      ],
      Page = 0,
      PageSize = 10
    };

    var osOnlineResult = await controller.SearchDevices(
      osOnlineRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    Assert.NotNull(multiFilterResult.Value);
    Assert.NotNull(multiFilterResult.Value.Items);
    Assert.Equal(2, multiFilterResult.Value.Items.Count);
    Assert.All(multiFilterResult.Value.Items, device =>
    {
      Assert.True(device.IsOnline);
      Assert.True(device.CpuUtilization > 0.5);
      Assert.Contains("Production", device.Name);
    });

    Assert.NotNull(osOnlineResult.Value);
    Assert.NotNull(osOnlineResult.Value.Items);
    Assert.Equal(3, osOnlineResult.Value.Items.Count);
    Assert.All(osOnlineResult.Value.Items, device =>
    {
      Assert.True(device.IsOnline);
      Assert.Contains("Windows Server", device.OsDescription);
    });
  }

  [Fact]
  public async Task GetDevicesGridData_FilterByNumericProperties()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with varying numeric properties
    var cpuValues = new[] { 0.1, 0.3, 0.5, 0.7, 0.9 };
    for (int i = 0; i < cpuValues.Length; i++)
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: cpuValues[i],
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192 + (i * 1000),
        UsedStorage: 512000 + (i * 100000),
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: $"192.168.0.{i}",
        LocalIpV6: $"fe80::{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"conn-{i}",
        RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: true);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    await controller.SetControllerUser(user, userManager);
    var cpuGtRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.GreaterThan, Value = "0.5" }],
      Page = 0,
      PageSize = 10
    };

    var cpuGtResult = await controller.SearchDevices(
      cpuGtRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var cpuEqRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.Equal, Value = "0.3" }],
      Page = 0,
      PageSize = 10
    };

    var cpuEqResult = await controller.SearchDevices(
      cpuEqRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    var cpuLteRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.LessThanOrEqual, Value = "0.5" }],
      Page = 0,
      PageSize = 10
    };

    var cpuLteResult = await controller.SearchDevices(
      cpuLteRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    var memGteRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "UsedMemoryPercent", Operator = FilterOperator.Number.GreaterThanOrEqual, Value = "0.6" }],
      Page = 0,
      PageSize = 10
    };

    var memGteResult = await controller.SearchDevices(
      memGteRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    var storageNeRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "UsedStoragePercent", Operator = FilterOperator.Number.NotEqual, Value = "0.5" }],
      Page = 0,
      PageSize = 10
    };

    var storageNeResult = await controller.SearchDevices(
      storageNeRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    Assert.NotNull(cpuGtResult.Value);
    Assert.NotNull(cpuGtResult.Value.Items);
    Assert.Equal(2, cpuGtResult.Value.Items.Count);
    Assert.All(cpuGtResult.Value.Items, device => Assert.True(device.CpuUtilization > 0.5));

    Assert.NotNull(cpuEqResult.Value);
    Assert.NotNull(cpuEqResult.Value.Items);
    Assert.Single(cpuEqResult.Value.Items);
    Assert.Equal(0.3, cpuEqResult.Value.Items[0].CpuUtilization);

    Assert.NotNull(cpuLteResult.Value);
    Assert.NotNull(cpuLteResult.Value.Items);
    Assert.Equal(3, cpuLteResult.Value.Items.Count);
    Assert.All(cpuLteResult.Value.Items, device => Assert.True(device.CpuUtilization <= 0.5));

    Assert.NotNull(memGteResult.Value);
    Assert.NotNull(memGteResult.Value.Items);
    Assert.True(memGteResult.Value.Items.Count >= 1);
    Assert.All(memGteResult.Value.Items, device => Assert.True(device.UsedMemoryPercent >= 0.6));

    Assert.NotNull(storageNeResult.Value);
    Assert.NotNull(storageNeResult.Value.Items);
    Assert.All(storageNeResult.Value.Items, device => Assert.NotEqual(0.5, device.UsedStoragePercent));
  }

  [Fact]
  public async Task GetDevicesGridData_FilterByStringProperties_Contains()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    // Create test tenant and user
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create test devices with varying string properties
    var devices = new[]
    {
      new { Name = "Windows Server", Alias = "WinSrv01", OsDescription = "Windows Server 2022", ConnectionId = "conn-001" },
      new { Name = "Linux Machine", Alias = "LinuxBox", OsDescription = "Ubuntu 22.04 LTS", ConnectionId = "conn-002" },
      new { Name = "Mac Workstation", Alias = "MacBook", OsDescription = "macOS Monterey", ConnectionId = "conn-003" }
    };

    for (int i = 0; i < devices.Length; i++)
    {
      var device = devices[i];
      var deviceDto = new DeviceUpdateRequestDto(
        Name: device.Name,
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: device.OsDescription,
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: $"192.168.0.{i}",
        LocalIpV6: $"fe80::{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: device.ConnectionId,
        RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: true);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    await controller.SetControllerUser(user, userManager);
    var nameRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.Contains, Value = "Server" }],
      Page = 0,
      PageSize = 10
    };

    var nameResult = await controller.SearchDevices(
      nameRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var osRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "OsDescription", Operator = FilterOperator.String.Contains, Value = "Ubuntu" }],
      Page = 0,
      PageSize = 10
    };

    var osResult = await controller.SearchDevices(
      osRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var connRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "ConnectionId", Operator = FilterOperator.String.Equal, Value = "conn-003" }],
      Page = 0,
      PageSize = 10
    };

    var connResult = await controller.SearchDevices(
      connRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    Assert.NotNull(nameResult.Value);
    Assert.NotNull(nameResult.Value.Items);
    Assert.Single(nameResult.Value.Items);
    Assert.Equal("Windows Server", nameResult.Value.Items[0].Name);

    Assert.NotNull(osResult.Value);
    Assert.NotNull(osResult.Value.Items);
    Assert.Single(osResult.Value.Items);
    Assert.Equal("Linux Machine", osResult.Value.Items[0].Name);

    Assert.NotNull(connResult.Value);
    Assert.NotNull(connResult.Value.Items);
    Assert.Single(connResult.Value.Items);
    Assert.Equal("Mac Workstation", connResult.Value.Items[0].Name);
  }

  [Fact]
  public async Task GetDevicesGridData_FilterByStringProperties_Various()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with specific patterns for testing
    var testDevices = new[]
    {
      new { Name = "Device-Prod-01", Alias = "Production Server", OsDescription = "Windows 11" },
      new { Name = "Device-Test-02", Alias = "", OsDescription = "Windows 10" },
      new { Name = "Device-Dev-03", Alias = "Development Box", OsDescription = "" }
    };

    for (int i = 0; i < testDevices.Length; i++)
    {
      var device = testDevices[i];
      var deviceDto = new DeviceUpdateRequestDto(
        Name: device.Name,
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: device.OsDescription,
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: $"192.168.0.{i}",
        LocalIpV6: $"fe80::{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"conn-{i:D3}",
        RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: true);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    // Manually set the Alias values since DeviceManager ignores DeviceDto.Alias
    var devices = await db.Devices.Where(d => d.TenantId == tenant.Id).ToListAsync(TestContext.Current.CancellationToken);
    for (int i = 0; i < devices.Count; i++)
    {
      devices[i].Alias = testDevices[i].Alias;
    }
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    await controller.SetControllerUser(user, userManager);
    var startsWithRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.StartsWith, Value = "Device-Prod" }],
      Page = 0,
      PageSize = 10
    };

    var startsWithResult = await controller.SearchDevices(
      startsWithRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var endsWithRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.EndsWith, Value = "-03" }],
      Page = 0,
      PageSize = 10
    };

    var endsWithResult = await controller.SearchDevices(
      endsWithRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var notContainsRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.NotContains, Value = "Prod" }],
      Page = 0,
      PageSize = 10
    };

    var notContainsResult = await controller.SearchDevices(
      notContainsRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var emptyRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Alias", Operator = FilterOperator.String.Empty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var emptyResult = await controller.SearchDevices(
      emptyRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var notEmptyRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Alias", Operator = FilterOperator.String.NotEmpty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var notEmptyResult = await controller.SearchDevices(
      notEmptyRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    Assert.NotNull(startsWithResult.Value);
    Assert.NotNull(startsWithResult.Value.Items);
    Assert.Single(startsWithResult.Value.Items);
    Assert.Equal("Device-Prod-01", startsWithResult.Value.Items[0].Name);

    Assert.NotNull(endsWithResult.Value);
    Assert.NotNull(endsWithResult.Value.Items);
    Assert.Single(endsWithResult.Value.Items);
    Assert.Equal("Device-Dev-03", endsWithResult.Value.Items[0].Name);

    Assert.NotNull(notContainsResult.Value);
    Assert.NotNull(notContainsResult.Value.Items);
    Assert.Equal(2, notContainsResult.Value.Items.Count);
    Assert.DoesNotContain(notContainsResult.Value.Items, d => d.Name.Contains("Prod"));

    Assert.NotNull(emptyResult.Value);
    Assert.NotNull(emptyResult.Value.Items);
    Assert.Single(emptyResult.Value.Items);
    Assert.Equal("Device-Test-02", emptyResult.Value.Items[0].Name);

    Assert.NotNull(notEmptyResult.Value);
    Assert.NotNull(notEmptyResult.Value.Items);
    Assert.Equal(2, notEmptyResult.Value.Items.Count);
    Assert.All(notEmptyResult.Value.Items, d => Assert.False(string.IsNullOrWhiteSpace(d.Alias)));
  }

  [Fact]
  public async Task GetDevicesGridData_FilterWithEmptyAndNotEmptyNumericValues()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with 0 and non-zero CPU utilization
    var cpuValues = new[] { 0.0, 0.5, 0.0, 0.8 };
    for (int i = 0; i < cpuValues.Length; i++)
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: cpuValues[i],
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        LocalIpV4: $"192.168.0.{i}",
        LocalIpV6: $"fe80::{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"conn-{i}",
        RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
        LastSeen: DateTimeOffset.Now,
        IsOnline: true);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    await controller.SetControllerUser(user, userManager);

    // Test Empty filter (CpuUtilization = 0)
    var emptyRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.Empty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var emptyResult = await controller.SearchDevices(
      emptyRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Test NotEmpty filter (CpuUtilization != 0)
    var notEmptyRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.NotEmpty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var notEmptyResult = await controller.SearchDevices(
      notEmptyRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    Assert.NotNull(emptyResult.Value);
    Assert.NotNull(emptyResult.Value.Items);
    Assert.Equal(2, emptyResult.Value.Items.Count);
    Assert.All(emptyResult.Value.Items, device => Assert.Equal(0.0, device.CpuUtilization));

    Assert.NotNull(notEmptyResult.Value);
    Assert.NotNull(notEmptyResult.Value.Items);
    Assert.Equal(2, notEmptyResult.Value.Items.Count);
    Assert.All(notEmptyResult.Value.Items, device => Assert.NotEqual(0.0, device.CpuUtilization));
  }

  [Fact]
  public async Task GetDevicesGridData_FilterWithInvalidValues()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create a test device
    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 0.5,
      Id: Guid.NewGuid(),
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      OsDescription: "Windows 11",
      TenantId: tenant.Id,
      TotalMemory: 16384,
      TotalStorage: 1024000,
      UsedMemory: 8192,
      UsedStorage: 512000,
      CurrentUsers: ["User1"],
      MacAddresses: ["00:00:00:00:00:01"],
      LocalIpV4: "192.168.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "conn-001",
      RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
      LastSeen: DateTimeOffset.Now,
      IsOnline: true);

    await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    await controller.SetControllerUser(user, userManager);
    var invalidNumericRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.Equal, Value = "invalid-number" }],
      Page = 0,
      PageSize = 10
    };

    var invalidNumericResult = await controller.SearchDevices(
      invalidNumericRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var invalidBooleanRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "maybe" }],
      Page = 0,
      PageSize = 10
    };

    var invalidBooleanResult = await controller.SearchDevices(
      invalidBooleanRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    var invalidPropertyRequest = new InternalDtos.DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "NonExistentProperty", Operator = FilterOperator.String.Equal, Value = "test" }],
      Page = 0,
      PageSize = 10
    };

    var invalidPropertyResult = await controller.SearchDevices(
      invalidPropertyRequest,
      db,
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());
    Assert.NotNull(invalidNumericResult.Value);
    Assert.NotNull(invalidNumericResult.Value.Items);
    Assert.Single(invalidNumericResult.Value.Items);

    Assert.NotNull(invalidBooleanResult.Value);
    Assert.NotNull(invalidBooleanResult.Value.Items);
    Assert.Single(invalidBooleanResult.Value.Items);

    Assert.NotNull(invalidPropertyResult.Value);
    Assert.NotNull(invalidPropertyResult.Value.Items);
    Assert.Single(invalidPropertyResult.Value.Items);
  }

  [Fact]
  public async Task GetDevicesGridData_RespectsUserAuthorization()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    // Create two tenants
    var tenant1 = await services.CreateTestTenant("Tenant 1");
    var tenant2 = await services.CreateTestTenant("Tenant 2");

    // Create user for tenant 1
    var user1 = await services.CreateTestUser(tenant1.Id, email: "user1@example.com", roles: RoleNames.DeviceSuperUser);

    // Create devices for both tenants
    for (int i = 0; i < 5; i++)
    {
      // Tenant 1 device
      var device1Id = Guid.NewGuid();
      var device1Dto = new DeviceUpdateRequestDto(
          Name: $"Tenant1 Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: device1Id,
          Is64Bit: true,
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          OsDescription: "Windows 10",
          TenantId: tenant1.Id,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192,
          UsedStorage: 512000,
          CurrentUsers: ["User1"],
          MacAddresses: ["00:00:00:00:00:01"],
          LocalIpV4: $"192.168.0.{i}",
          LocalIpV6: $"fe80::{i}",
          Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext1 = new DeviceConnectionContext(
          ConnectionId: $"tenant1-connection-{i}",
          RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
          LastSeen: DateTimeOffset.Now,
          IsOnline: true);

      await deviceManager.AddOrUpdate(device1Dto, connectionContext1);

      // Tenant 2 device
      var device2Id = Guid.NewGuid();
      var device2Dto = new DeviceUpdateRequestDto(
          Name: $"Tenant2 Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: device2Id,
          Is64Bit: true,
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          OsDescription: "Windows 10",
          TenantId: tenant2.Id,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192,
          UsedStorage: 512000,
          CurrentUsers: ["User2"],
          MacAddresses: ["00:00:00:00:00:02"],
          LocalIpV4: $"192.168.0.{i}",
          LocalIpV6: $"fe80::{i}",
          Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext2 = new DeviceConnectionContext(
          ConnectionId: $"tenant2-connection-{i}",
          RemoteIpAddress: IPAddress.Parse("192.168.1.2"),
          LastSeen: DateTimeOffset.Now,
          IsOnline: true);

      await deviceManager.AddOrUpdate(device2Dto, connectionContext2);
    }

    // Configure controller user context for authorization
    await controller.SetControllerUser(user1, userManager);

    // Act
    var request = new InternalDtos.DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 20
    };

    var result = await controller.SearchDevices(
        request,
        db,
        services.GetRequiredService<IAgentVersionProvider>(),
        services.GetRequiredService<ILogger<DevicesController>>());
    var response = result.Value;

    // Assert
    Assert.NotNull(response);
    Assert.NotNull(response.Items);
    Assert.Equal(5, response.Items.Count);
    Assert.All(response.Items, device => Assert.Equal(tenant1.Id, device.TenantId));
    Assert.All(response.Items, device => Assert.StartsWith("Tenant1", device.Name));
  }

  [Fact]
  public async Task GetDevicesGridData_ReturnsCorrectDevices()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var logger = services.GetRequiredService<ILogger<DevicesController>>();
    var authorizationService = services.GetRequiredService<IAuthorizationService>();
    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();

    // Create test tenant and user
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create test tags
    var tagIds = new[] { Guid.NewGuid(), Guid.NewGuid() }.ToImmutableArray();
    foreach (var tagId in tagIds)
    {
      db.Tags.Add(new Tag { Id = tagId, Name = $"Tag {tagId}", TenantId = tenant.Id });
    }
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Create test devices
    for (int i = 0; i < 10; i++)
    {
      var deviceId = Guid.NewGuid();
      var deviceDto = new DeviceUpdateRequestDto(
          Name: $"Test Device {i}",
          AgentVersion: "1.0.0",
          CpuUtilization: i * 10,
          Id: deviceId,
          Is64Bit: true,
          OsArchitecture: Architecture.X64,
          Platform: SystemPlatform.Windows,
          ProcessorCount: 8,
          OsDescription: $"Windows {10 + i}",
          TenantId: tenant.Id,
          TotalMemory: 16384,
          TotalStorage: 1024000,
          UsedMemory: 8192 + (i * 100),
          UsedStorage: 512000 + (i * 1000),
          CurrentUsers: [$"User{i}"],
          MacAddresses: [$"00:00:00:00:00:{i:D2}"],
          LocalIpV4: $"192.168.0.{i}",
          LocalIpV6: $"fe80::{i}",
          Drives: [new Drive { Name = $"C{i}", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 - (i * 1000) }]);

      var connectionContext = new DeviceConnectionContext(
          ConnectionId: $"test-connection-id-{i}",
          RemoteIpAddress: IPAddress.Parse($"192.168.1.{i}"),
          LastSeen: DateTimeOffset.Now.AddMinutes(-i),
          IsOnline: i % 2 == 0);

      Guid[]? devTagIds = i % 3 == 0 ? [.. tagIds] : null;
      await deviceManager.AddOrUpdate(deviceDto, connectionContext, devTagIds);
    }
    await controller.SetControllerUser(user, userManager);

    // Act
    // Test case 1: Get all devices with pagination
    var request1 = new InternalDtos.DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 5
    };

    var result1 = await controller.SearchDevices(
        request1,
        db,
        agentVersionProvider,
        logger);

    var response1 = result1.Value;

    // Test case 2: Filter by online status
    var request2 = new InternalDtos.DeviceSearchRequestDto
    {
      HideOfflineDevices = true,
      Page = 0,
      PageSize = 10
    };

    var result2 = await controller.SearchDevices(
        request2,
        db,
        agentVersionProvider,
        logger);

    var response2 = result2.Value;

    // Test case 3: Filter by tag
    var request3 = new InternalDtos.DeviceSearchRequestDto
    {
      TagIds = [tagIds[0]],
      Page = 0,
      PageSize = 10
    };

    var result3 = await controller.SearchDevices(
        request3,
        db,
        agentVersionProvider,
        logger);

    var response3 = result3.Value;

    // Test case 4: Search by name
    var request4 = new InternalDtos.DeviceSearchRequestDto
    {
      SearchText = "Device 1",
      Page = 0,
      PageSize = 10
    };

    var result4 = await controller.SearchDevices(
        request4,
        db,
        agentVersionProvider,
        logger);
    var response4 = result4.Value;

    // Test case 5: Sort by CPU utilization (descending)
    var request5 = new InternalDtos.DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 10,
      SortDefinitions = [new DeviceColumnSort { PropertyName = "CpuUtilization", Descending = true, SortOrder = 0 }]
    };

    var result5 = await controller.SearchDevices(
        request5,
        db,
        agentVersionProvider,
        logger);

    var response5 = result5.Value;

    // Test case 6: Filter by selected tag plus untagged devices
    var request6 = new InternalDtos.DeviceSearchRequestDto
    {
      TagIds = [tagIds[0]],
      IncludeUntaggedDevices = true,
      Page = 0,
      PageSize = 20
    };

    var result6 = await controller.SearchDevices(
        request6,
        db,
        agentVersionProvider,
        logger);

    var response6 = result6.Value;

    // Test case 7: Filter by untagged devices only
    var request7 = new InternalDtos.DeviceSearchRequestDto
    {
      IncludeUntaggedDevices = true,
      Page = 0,
      PageSize = 20
    };

    var result7 = await controller.SearchDevices(
        request7,
        db,
        agentVersionProvider,
        logger);

    var response7 = result7.Value;

    // Assert
    // Test case 1: Pagination
    Assert.NotNull(response1);
    Assert.NotNull(response1.Items);
    Assert.Equal(5, response1.Items.Count);
    Assert.Equal(10, response1.TotalItems);
    Assert.NotNull(response1.FilterCounts);
    Assert.Equal(5, response1.FilterCounts.OnlineDevices);
    Assert.Equal(5, response1.FilterCounts.OfflineDevices);
    Assert.Equal(4, response1.FilterCounts.TaggedDevices);
    Assert.Equal(6, response1.FilterCounts.UntaggedDevices);
    Assert.Equal(6, response1.HiddenUntaggedDevices);

    // Test case 2: Filter by online status
    Assert.NotNull(response2);
    Assert.NotNull(response2.Items);
    Assert.All(response2.Items, device => Assert.True(device.IsOnline));
    Assert.Equal(5, response2.Items.Count);
    Assert.Equal(5, response2.FilterCounts.OnlineDevices);
    Assert.Equal(0, response2.FilterCounts.OfflineDevices);
    Assert.Equal(2, response2.FilterCounts.TaggedDevices);
    Assert.Equal(3, response2.FilterCounts.UntaggedDevices);
    Assert.Equal(3, response2.HiddenUntaggedDevices);
    Assert.NotNull(response3);
    Assert.NotNull(response3.Items);
    Assert.All(response3.Items, device => Assert.NotNull(device.TagIds));
    Assert.All(response3.Items, device => Assert.Contains(tagIds[0], device.TagIds!));
    Assert.Equal(4, response3.TotalItems);
    Assert.Equal(4, response3.FilterCounts.TaggedDevices);
    Assert.Equal(0, response3.FilterCounts.UntaggedDevices);
    Assert.Equal(6, response3.HiddenUntaggedDevices);

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

    // Test case 6: Selected tag plus untagged devices
    Assert.NotNull(response6);
    Assert.NotNull(response6.Items);
    Assert.Equal(10, response6.Items.Count);
    Assert.Equal(5, response6.FilterCounts.OnlineDevices);
    Assert.Equal(5, response6.FilterCounts.OfflineDevices);
    Assert.Equal(4, response6.FilterCounts.TaggedDevices);
    Assert.Equal(6, response6.FilterCounts.UntaggedDevices);
    Assert.Equal(0, response6.HiddenUntaggedDevices);
    Assert.Contains(response6.Items, device => device.TagIds is { Length: 0 });
    Assert.Contains(response6.Items, device => device.TagIds?.Contains(tagIds[0]) == true);

    // Test case 7: Untagged devices only
    Assert.NotNull(response7);
    Assert.NotNull(response7.Items);
    Assert.Equal(6, response7.Items.Count);
    Assert.Equal(0, response7.FilterCounts.TaggedDevices);
    Assert.Equal(6, response7.FilterCounts.UntaggedDevices);
    Assert.Equal(0, response7.HiddenUntaggedDevices);
    Assert.All(response7.Items, device => Assert.True(device.TagIds is null or { Length: 0 }));
  }

  [Fact]
  public async Task GetDeviceSummaries_RespectsTenantBoundary()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Cross-Tenant Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: Guid.NewGuid(),
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 4,
      OsDescription: "Windows 10",
      TenantId: Guid.NewGuid(),
      TotalMemory: 8192,
      TotalStorage: 256000,
      UsedMemory: 4096,
      UsedStorage: 128000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:11:22:33:44:55"],
      LocalIpV4: "10.0.0.2",
      LocalIpV6: "fe80::2",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 256000, FreeSpace = 128000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-conn",
      RemoteIpAddress: IPAddress.Loopback,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: true);

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    await deviceManager.AddOrUpdate(deviceDto, connectionContext);

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());

    // Act
    var results = new List<InternalDtos.DeviceSummaryDto>();
    await foreach (var summary in controller.GetDeviceSummaries(db))
    {
      results.Add(summary);
    }

    // Assert
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetDeviceSummaries_ReturnsEmptyWhenNoDevices()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    await controller.SetControllerUser(user, services.GetRequiredService<UserManager<AppUser>>());

    // Act
    var results = new List<InternalDtos.DeviceSummaryDto>();
    await foreach (var summary in controller.GetDeviceSummaries(db))
    {
      results.Add(summary);
    }

    // Assert
    Assert.Empty(results);
  }

  [Fact]
  public async Task GetDevices_StreamOnlyAuthorizedDevices()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();

    var tenant = await services.CreateTestTenant();
    _ = await services.CreateTestUser(tenant.Id, email: "seed-user-stream@test.local");
    var user = await services.CreateTestUser(tenant.Id, email: "tagged-user-stream@test.local");

    var allowedTag = new Tag { Id = Guid.NewGuid(), Name = "Allowed", TenantId = tenant.Id };
    var blockedTag = new Tag { Id = Guid.NewGuid(), Name = "Blocked", TenantId = tenant.Id };
    db.Tags.AddRange(allowedTag, blockedTag);

    var persistedUser = await db.Users
      .Include(x => x.Tags)
      .FirstAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);
    persistedUser.Tags ??= [];
    persistedUser.Tags.Add(allowedTag);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    for (var i = 0; i < 3; i++)
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Scoped Stream Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: i + 1,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: [$"User{i}"],
        MacAddresses: [$"00:00:00:00:10:{i:D2}"],
        LocalIpV4: $"10.0.0.{i}",
        LocalIpV6: $"fe80::10:{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"stream-auth-{i}",
        RemoteIpAddress: IPAddress.Loopback,
        LastSeen: DateTimeOffset.UtcNow,
        IsOnline: true);

      Guid[]? tagIds = i switch
      {
        0 => [allowedTag.Id],
        1 => [blockedTag.Id],
        _ => null
      };

      await deviceManager.AddOrUpdate(deviceDto, connectionContext, tagIds);
    }

    await controller.SetControllerUser(user, userManager);

    var results = new List<InternalDtos.DeviceResponseDto>();
    await foreach (var device in controller.Get(db, agentVersionProvider))
    {
      results.Add(device);
    }

    Assert.Single(results);
    Assert.All(results, device => Assert.Contains(allowedTag.Id, device.TagIds!));
  }

  [Fact]
  public async Task SearchDevices_FiltersCountsToAuthorizedDevices()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();
    var logger = services.GetRequiredService<ILogger<DevicesController>>();

    var tenant = await services.CreateTestTenant();
    _ = await services.CreateTestUser(tenant.Id, email: "seed-user@test.local");
    var user = await services.CreateTestUser(tenant.Id, email: "tagged-user@test.local");

    var allowedTag = new Tag { Id = Guid.NewGuid(), Name = "Allowed", TenantId = tenant.Id };
    var blockedTag = new Tag { Id = Guid.NewGuid(), Name = "Blocked", TenantId = tenant.Id };
    db.Tags.AddRange(allowedTag, blockedTag);

    var persistedUser = await db.Users
      .Include(x => x.Tags)
      .FirstAsync(x => x.Id == user.Id, TestContext.Current.CancellationToken);
    persistedUser.Tags ??= [];
    persistedUser.Tags.Add(allowedTag);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    for (var i = 0; i < 3; i++)
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Scoped Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: i + 1,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: [$"User{i}"],
        MacAddresses: [$"00:00:00:00:00:{i:D2}"],
        LocalIpV4: $"192.168.50.{i}",
        LocalIpV6: $"fe80::{i}",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"auth-test-{i}",
        RemoteIpAddress: IPAddress.Loopback,
        LastSeen: DateTimeOffset.UtcNow,
        IsOnline: i == 0);

      Guid[]? tagIds = i switch
      {
        0 => [allowedTag.Id],
        1 => [blockedTag.Id],
        _ => null
      };

      await deviceManager.AddOrUpdate(deviceDto, connectionContext, tagIds);
    }

    await controller.SetControllerUser(user, userManager);

    var result = await controller.SearchDevices(
      new InternalDtos.DeviceSearchRequestDto
      {
        Page = 0,
        PageSize = 20
      },
      db,
      agentVersionProvider,
      logger);

    var response = result.Value;

    Assert.NotNull(response);
    Assert.True(response.AnyDevicesForUser);
    Assert.NotNull(response.Items);
    Assert.Single(response.Items);
    Assert.Equal(1, response.TotalItems);
    Assert.Equal(1, response.FilterCounts.TaggedDevices);
    Assert.Equal(0, response.FilterCounts.UntaggedDevices);
    Assert.Equal(1, response.FilterCounts.OnlineDevices);
    Assert.Equal(0, response.FilterCounts.OfflineDevices);
    Assert.Equal(0, response.HiddenUntaggedDevices);
    Assert.All(response.Items, device => Assert.Contains(allowedTag.Id, device.TagIds!));
  }

  [Fact]
  public async Task SearchDevices_LogonTokenSession_IsRestrictedToScopedDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceManager = services.GetRequiredService<IDeviceManager>();
    var authorizationService = services.GetRequiredService<IAuthorizationService>();
    var agentVersionProvider = services.GetRequiredService<IAgentVersionProvider>();
    var logger = services.GetRequiredService<ILogger<DevicesController>>();

    var tenant = await services.CreateTestTenant();
    _ = await services.CreateTestUser(tenant.Id, email: "seed-logon-scope@test.local");

    var scopedDeviceId = Guid.NewGuid();
    var otherDeviceId = Guid.NewGuid();

    foreach (var deviceId in new[] { scopedDeviceId, otherDeviceId })
    {
      var deviceDto = new DeviceUpdateRequestDto(
        Name: $"Scoped Bulk Device {deviceId}",
        AgentVersion: "1.0.0",
        CpuUtilization: 5,
        Id: deviceId,
        Is64Bit: true,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["ScopedUser"],
        MacAddresses: [$"00:00:00:00:{(deviceId == scopedDeviceId ? "20" : "21")}:01"],
        LocalIpV4: deviceId == scopedDeviceId ? "10.10.0.1" : "10.10.0.2",
        LocalIpV6: deviceId == scopedDeviceId ? "fe80::201" : "fe80::202",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      var connectionContext = new DeviceConnectionContext(
        ConnectionId: $"logon-scope-{deviceId}",
        RemoteIpAddress: IPAddress.Loopback,
        LastSeen: DateTimeOffset.UtcNow,
        IsOnline: true);

      await deviceManager.AddOrUpdate(deviceDto, connectionContext);
    }

    var claims = new List<Claim>
    {
      new(UserClaimTypes.TenantId, tenant.Id.ToString()),
      new(UserClaimTypes.AuthenticationMethod, LogonTokenAuthenticationSchemeOptions.DefaultScheme),
      new(UserClaimTypes.DeviceSessionScope, scopedDeviceId.ToString())
    };

    controller.ControllerContext = new ControllerContext
    {
      HttpContext = new DefaultHttpContext
      {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"))
      }
    };

    var result = await controller.SearchDevices(
      new InternalDtos.DeviceSearchRequestDto
      {
        Page = 0,
        PageSize = 20
      },
      db,
      agentVersionProvider,
      logger);

    var response = result.Value;

    Assert.NotNull(response);
    Assert.True(response.AnyDevicesForUser);
    Assert.NotNull(response.Items);
    Assert.Single(response.Items);
    Assert.Equal(scopedDeviceId, response.Items[0].Id);
    Assert.Equal(1, response.TotalItems);
    Assert.Equal(0, response.FilterCounts.TaggedDevices);
    Assert.Equal(1, response.FilterCounts.UntaggedDevices);
    Assert.Equal(1, response.FilterCounts.OnlineDevices);
    Assert.Equal(0, response.FilterCounts.OfflineDevices);
    Assert.Equal(1, response.HiddenUntaggedDevices);
  }

  [Fact]
  public async Task UpdateDeviceAlias_AliasTooLong_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceId = Guid.NewGuid();
    var longAlias = new string('x', 101);
    var request = new InternalDtos.UpdateDeviceAliasRequestDto(deviceId, longAlias);

    var result = await controller.UpdateDeviceAlias(
      deviceId,
      request,
      db,
      services.GetRequiredService<IAuthorizationService>(),
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task UpdateDeviceAlias_DeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var deviceId = Guid.NewGuid();
    var request = new InternalDtos.UpdateDeviceAliasRequestDto(deviceId, "Alias");

    var result = await controller.UpdateDeviceAlias(
      deviceId,
      request,
      db,
      services.GetRequiredService<IAuthorizationService>(),
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    Assert.IsType<NotFoundResult>(result.Result);
  }

  [Fact]
  public async Task UpdateDeviceAlias_NullAlias_NormalizesToEmptyString()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var deviceManager = services.GetRequiredService<IDeviceManager>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    var deviceId = Guid.NewGuid();
    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: deviceId,
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      OsDescription: "Windows 10",
      TenantId: tenant.Id,
      TotalMemory: 16384,
      TotalStorage: 1024000,
      UsedMemory: 8192,
      UsedStorage: 512000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:00:00:00:00:01"],
      LocalIpV4: "192.168.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-conn",
      RemoteIpAddress: IPAddress.Loopback,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: true);

    await deviceManager.AddOrUpdate(deviceDto, connectionContext);

    await controller.SetControllerUser(user, userManager);

    var request = new InternalDtos.UpdateDeviceAliasRequestDto(deviceId, null);

    var result = await controller.UpdateDeviceAlias(
      deviceId,
      request,
      db,
      services.GetRequiredService<IAuthorizationService>(),
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    Assert.NotNull(result.Value);
    Assert.Equal(string.Empty, result.Value.Alias);
  }

  [Fact]
  public async Task UpdateDeviceAlias_RouteIdMismatch_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var routeId = Guid.NewGuid();
    var bodyId = Guid.NewGuid();
    var request = new InternalDtos.UpdateDeviceAliasRequestDto(bodyId, "Alias");

    var result = await controller.UpdateDeviceAlias(
      routeId,
      request,
      db,
      services.GetRequiredService<IAuthorizationService>(),
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task UpdateDeviceAlias_UserWithoutAccess_ReturnsForbid()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var deviceManager = services.GetRequiredService<IDeviceManager>();

    // Create device in one tenant
    var deviceTenant = await services.CreateTestTenant("Device Tenant");
    var deviceId = Guid.NewGuid();
    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: deviceId,
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      OsDescription: "Windows 10",
      TenantId: deviceTenant.Id,
      TotalMemory: 16384,
      TotalStorage: 1024000,
      UsedMemory: 8192,
      UsedStorage: 512000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:00:00:00:00:01"],
      LocalIpV4: "192.168.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-conn",
      RemoteIpAddress: IPAddress.Loopback,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: true);

    await deviceManager.AddOrUpdate(deviceDto, connectionContext);

    // Create user in a different tenant
    var userTenant = await services.CreateTestTenant("User Tenant");
    var user = await services.CreateTestUser(userTenant.Id, roles: RoleNames.DeviceSuperUser);

    await controller.SetControllerUser(user, userManager);

    var request = new InternalDtos.UpdateDeviceAliasRequestDto(deviceId, "Alias");

    var result = await controller.UpdateDeviceAlias(
      deviceId,
      request,
      db,
      services.GetRequiredService<IAuthorizationService>(),
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task UpdateDeviceAlias_WithValidAlias_ReturnsUpdatedDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<DevicesController>();
    await using var db = services.GetRequiredService<AppDb>();

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var deviceManager = services.GetRequiredService<IDeviceManager>();

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    var deviceId = Guid.NewGuid();
    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: deviceId,
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      OsDescription: "Windows 10",
      TenantId: tenant.Id,
      TotalMemory: 16384,
      TotalStorage: 1024000,
      UsedMemory: 8192,
      UsedStorage: 512000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:00:00:00:00:01"],
      LocalIpV4: "192.168.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-conn",
      RemoteIpAddress: IPAddress.Loopback,
      LastSeen: DateTimeOffset.UtcNow,
      IsOnline: true);

    await deviceManager.AddOrUpdate(deviceDto, connectionContext);

    await controller.SetControllerUser(user, userManager);

    var request = new InternalDtos.UpdateDeviceAliasRequestDto(deviceId, "My Alias");

    var result = await controller.UpdateDeviceAlias(
      deviceId,
      request,
      db,
      services.GetRequiredService<IAuthorizationService>(),
      services.GetRequiredService<IAgentVersionProvider>(),
      services.GetRequiredService<ILogger<DevicesController>>());

    Assert.NotNull(result.Value);
    Assert.Equal("My Alias", result.Value.Alias);
    Assert.Equal(deviceId, result.Value.Id);
  }
}
