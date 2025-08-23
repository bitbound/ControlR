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
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
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

    // Create test tenant and user
    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create test tag
    var tagId = Guid.NewGuid();
    db.Tags.Add(new Tag { Id = tagId, Name = "TestTag", TenantId = tenant.Id });
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
          TenantId: tenant.Id,
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
    }

    // Configure controller user context for authorization
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
  public async Task GetDevicesGridData_FilterByBooleanProperties()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with different online status
    for (int i = 0; i < 5; i++)
    {
      var deviceDto = new DeviceDto(
        Name: $"Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        IsOnline: i % 2 == 0, // Alternate online/offline
        LastSeen: DateTimeOffset.Now,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        ConnectionId: $"conn-{i}",
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        PublicIpV4: "192.168.1.1",
        PublicIpV6: "::1",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: false);
    }

    await controller.SetControllerUser(user, userManager);    // Test IsOnline = true filter
    var onlineRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "true" }],
      Page = 0,
      PageSize = 10
    };

    var onlineResult = await controller.SearchDevices(
      onlineRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test IsOnline = false filter
    var offlineRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "false" }],
      Page = 0,
      PageSize = 10
    };

    var offlineResult = await controller.SearchDevices(
      offlineRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    Assert.NotNull(onlineResult.Value);
    Assert.NotNull(onlineResult.Value.Items);
    Assert.Equal(3, onlineResult.Value.Items.Count); // Devices 0, 2, 4
    Assert.All(onlineResult.Value.Items, device => Assert.True(device.IsOnline));

    Assert.NotNull(offlineResult.Value);
    Assert.NotNull(offlineResult.Value.Items);
    Assert.Equal(2, offlineResult.Value.Items.Count); // Devices 1, 3
    Assert.All(offlineResult.Value.Items, device => Assert.False(device.IsOnline));
  }

  [Fact]
  public async Task GetDevicesGridData_FilterByMultipleColumns()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

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
      var deviceDto = new DeviceDto(
        Name: data.Name,
        AgentVersion: "1.0.0",
        CpuUtilization: data.CpuUtilization,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        IsOnline: data.IsOnline,
        LastSeen: DateTimeOffset.Now,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        ConnectionId: $"conn-{i}",
        OsDescription: data.OsDescription,
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        PublicIpV4: "192.168.1.1",
        PublicIpV6: "::1",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: false);
    }

    await controller.SetControllerUser(user, userManager);

    // Test multiple filters: Online + High CPU + Contains "Production"
    var multiFilterRequest = new DeviceSearchRequestDto
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
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());

    // Test OS + Online filters
    var osOnlineRequest = new DeviceSearchRequestDto
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
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    Assert.NotNull(multiFilterResult.Value);
    Assert.NotNull(multiFilterResult.Value.Items);
    Assert.Equal(2, multiFilterResult.Value.Items.Count); // Production-Web-01 and Production-DB-01
    Assert.All(multiFilterResult.Value.Items, device =>
    {
      Assert.True(device.IsOnline);
      Assert.True(device.CpuUtilization > 0.5);
      Assert.Contains("Production", device.Name);
    });

    Assert.NotNull(osOnlineResult.Value);
    Assert.NotNull(osOnlineResult.Value.Items);
    Assert.Equal(3, osOnlineResult.Value.Items.Count); // All Windows Server devices that are online
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
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with varying numeric properties
    var cpuValues = new double[] { 0.1, 0.3, 0.5, 0.7, 0.9 }; // 10%, 30%, 50%, 70%, 90%
    for (int i = 0; i < cpuValues.Length; i++)
    {
      var deviceDto = new DeviceDto(
        Name: $"Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: cpuValues[i],
        Id: Guid.NewGuid(),
        Is64Bit: true,
        IsOnline: true,
        LastSeen: DateTimeOffset.Now,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        ConnectionId: $"conn-{i}",
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192 + (i * 1000), // Varying memory usage
        UsedStorage: 512000 + (i * 100000), // Varying storage usage
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        PublicIpV4: "192.168.1.1",
        PublicIpV6: "::1",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: false);
    }

    await controller.SetControllerUser(user, userManager);    // Test CpuUtilization GreaterThan filter
    var cpuGtRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.GreaterThan, Value = "0.5" }],
      Page = 0,
      PageSize = 10
    };

    var cpuGtResult = await controller.SearchDevices(
      cpuGtRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test CpuUtilization Equal filter
    var cpuEqRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.Equal, Value = "0.3" }],
      Page = 0,
      PageSize = 10
    };

    var cpuEqResult = await controller.SearchDevices(
      cpuEqRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test CpuUtilization LessThanOrEqual filter
    var cpuLteRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.LessThanOrEqual, Value = "0.5" }],
      Page = 0,
      PageSize = 10
    };

    var cpuLteResult = await controller.SearchDevices(
      cpuLteRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test UsedMemoryPercent GreaterThanOrEqual filter
    var memGteRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "UsedMemoryPercent", Operator = FilterOperator.Number.GreaterThanOrEqual, Value = "0.6" }],
      Page = 0,
      PageSize = 10
    };

    var memGteResult = await controller.SearchDevices(
      memGteRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test UsedStoragePercent NotEqual filter
    var storageNeRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "UsedStoragePercent", Operator = FilterOperator.Number.NotEqual, Value = "0.5" }],
      Page = 0,
      PageSize = 10
    };

    var storageNeResult = await controller.SearchDevices(
      storageNeRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Assert
    Assert.NotNull(cpuGtResult.Value);
    Assert.NotNull(cpuGtResult.Value.Items);
    Assert.Equal(2, cpuGtResult.Value.Items.Count); // Devices with 0.7 and 0.9
    Assert.All(cpuGtResult.Value.Items, device => Assert.True(device.CpuUtilization > 0.5));

    Assert.NotNull(cpuEqResult.Value);
    Assert.NotNull(cpuEqResult.Value.Items);
    Assert.Single(cpuEqResult.Value.Items);
    Assert.Equal(0.3, cpuEqResult.Value.Items[0].CpuUtilization);

    Assert.NotNull(cpuLteResult.Value);
    Assert.NotNull(cpuLteResult.Value.Items);
    Assert.Equal(3, cpuLteResult.Value.Items.Count); // Devices with 0.1, 0.3, 0.5
    Assert.All(cpuLteResult.Value.Items, device => Assert.True(device.CpuUtilization <= 0.5));

    Assert.NotNull(memGteResult.Value);
    Assert.NotNull(memGteResult.Value.Items);
    Assert.True(memGteResult.Value.Items.Count >= 1);
    Assert.All(memGteResult.Value.Items, device => Assert.True(device.UsedMemoryPercent >= 0.6));

    Assert.NotNull(storageNeResult.Value);
    Assert.NotNull(storageNeResult.Value.Items);
    // Should return devices that don't have exactly 0.5 storage utilization
    Assert.All(storageNeResult.Value.Items, device => Assert.NotEqual(0.5, device.UsedStoragePercent));
  }

  [Fact]
  public async Task GetDevicesGridData_FilterByStringProperties_Contains()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    // Create test tenant and user
    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

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
      var deviceDto = new DeviceDto(
        Name: device.Name,
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        IsOnline: true,
        LastSeen: DateTimeOffset.Now,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        ConnectionId: device.ConnectionId,
        OsDescription: device.OsDescription,
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        PublicIpV4: "192.168.1.1",
        PublicIpV6: "::1",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }])
      {
        Alias = device.Alias
      };

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: false);
    }

    await controller.SetControllerUser(user, userManager);    // Test Name contains filter
    var nameRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.Contains, Value = "Server" }],
      Page = 0,
      PageSize = 10
    };

    var nameResult = await controller.SearchDevices(
      nameRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test OsDescription contains filter
    var osRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "OsDescription", Operator = FilterOperator.String.Contains, Value = "Ubuntu" }],
      Page = 0,
      PageSize = 10
    };

    var osResult = await controller.SearchDevices(
      osRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test ConnectionId equals filter
    var connRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "ConnectionId", Operator = FilterOperator.String.Equal, Value = "conn-003" }],
      Page = 0,
      PageSize = 10
    };

    var connResult = await controller.SearchDevices(
      connRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Assert
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
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

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
      var deviceDto = new DeviceDto(
        Name: device.Name,
        AgentVersion: "1.0.0",
        CpuUtilization: 50,
        Id: Guid.NewGuid(),
        Is64Bit: true,
        IsOnline: true,
        LastSeen: DateTimeOffset.Now,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        ConnectionId: $"conn-{i:D3}",
        OsDescription: device.OsDescription,
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        PublicIpV4: "192.168.1.1",
        PublicIpV6: "::1",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }])
      {
        Alias = device.Alias
      };

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: false);
    }

    // Manually set the Alias values since DeviceManager ignores DeviceDto.Alias
    var devices = await db.Devices.Where(d => d.TenantId == tenant.Id).ToListAsync();
    for (int i = 0; i < devices.Count; i++)
    {
      devices[i].Alias = testDevices[i].Alias;
    }
    await db.SaveChangesAsync();

    await controller.SetControllerUser(user, userManager);    // Test StartsWith filter
    var startsWithRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.StartsWith, Value = "Device-Prod" }],
      Page = 0,
      PageSize = 10
    };

    var startsWithResult = await controller.SearchDevices(
      startsWithRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test EndsWith filter
    var endsWithRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.EndsWith, Value = "-03" }],
      Page = 0,
      PageSize = 10
    };

    var endsWithResult = await controller.SearchDevices(
      endsWithRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test NotContains filter
    var notContainsRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Name", Operator = FilterOperator.String.NotContains, Value = "Prod" }],
      Page = 0,
      PageSize = 10
    };

    var notContainsResult = await controller.SearchDevices(
      notContainsRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test Empty filter for Alias
    var emptyRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Alias", Operator = FilterOperator.String.Empty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var emptyResult = await controller.SearchDevices(
      emptyRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test NotEmpty filter for Alias
    var notEmptyRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "Alias", Operator = FilterOperator.String.NotEmpty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var notEmptyResult = await controller.SearchDevices(
      notEmptyRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Assert
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
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create devices with 0 and non-zero CPU utilization
    var cpuValues = new double[] { 0.0, 0.5, 0.0, 0.8 };
    for (int i = 0; i < cpuValues.Length; i++)
    {
      var deviceDto = new DeviceDto(
        Name: $"Device {i}",
        AgentVersion: "1.0.0",
        CpuUtilization: cpuValues[i],
        Id: Guid.NewGuid(),
        Is64Bit: true,
        IsOnline: true,
        LastSeen: DateTimeOffset.Now,
        OsArchitecture: Architecture.X64,
        Platform: SystemPlatform.Windows,
        ProcessorCount: 8,
        ConnectionId: $"conn-{i}",
        OsDescription: "Windows 11",
        TenantId: tenant.Id,
        TotalMemory: 16384,
        TotalStorage: 1024000,
        UsedMemory: 8192,
        UsedStorage: 512000,
        CurrentUsers: ["User1"],
        MacAddresses: ["00:00:00:00:00:01"],
        PublicIpV4: "192.168.1.1",
        PublicIpV6: "::1",
        Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

      await deviceManager.AddOrUpdate(deviceDto, addTagIds: false);
    }

    await controller.SetControllerUser(user, userManager);

    // Test Empty filter (CpuUtilization = 0)
    var emptyRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.Empty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var emptyResult = await controller.SearchDevices(
      emptyRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());

    // Test NotEmpty filter (CpuUtilization != 0)
    var notEmptyRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.NotEmpty, Value = "" }],
      Page = 0,
      PageSize = 10
    };

    var notEmptyResult = await controller.SearchDevices(
      notEmptyRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());

    // Assert
    Assert.NotNull(emptyResult.Value);
    Assert.NotNull(emptyResult.Value.Items);
    Assert.Equal(2, emptyResult.Value.Items.Count); // Devices 0 and 2
    Assert.All(emptyResult.Value.Items, device => Assert.Equal(0.0, device.CpuUtilization));

    Assert.NotNull(notEmptyResult.Value);
    Assert.NotNull(notEmptyResult.Value.Items);
    Assert.Equal(2, notEmptyResult.Value.Items.Count); // Devices 1 and 3
    Assert.All(notEmptyResult.Value.Items, device => Assert.NotEqual(0.0, device.CpuUtilization));
  }

  [Fact]
  public async Task GetDevicesGridData_FilterWithInvalidValues()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create a test device
    var deviceDto = new DeviceDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 0.5,
      Id: Guid.NewGuid(),
      Is64Bit: true,
      IsOnline: true,
      LastSeen: DateTimeOffset.Now,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      ConnectionId: "conn-001",
      OsDescription: "Windows 11",
      TenantId: tenant.Id,
      TotalMemory: 16384,
      TotalStorage: 1024000,
      UsedMemory: 8192,
      UsedStorage: 512000,
      CurrentUsers: ["User1"],
      MacAddresses: ["00:00:00:00:00:01"],
      PublicIpV4: "192.168.1.1",
      PublicIpV6: "::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 1024000, FreeSpace = 512000 }]);

    await deviceManager.AddOrUpdate(deviceDto, addTagIds: false);
    await controller.SetControllerUser(user, userManager);    // Test invalid numeric value - should return all devices (filter ignored)
    var invalidNumericRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "CpuUtilization", Operator = FilterOperator.Number.Equal, Value = "invalid-number" }],
      Page = 0,
      PageSize = 10
    };

    var invalidNumericResult = await controller.SearchDevices(
      invalidNumericRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test invalid boolean value - should return all devices (filter ignored)
    var invalidBooleanRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "IsOnline", Operator = FilterOperator.Boolean.Is, Value = "maybe" }],
      Page = 0,
      PageSize = 10
    };

    var invalidBooleanResult = await controller.SearchDevices(
      invalidBooleanRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Test invalid property name - should be ignored gracefully
    var invalidPropertyRequest = new DeviceSearchRequestDto
    {
      FilterDefinitions = [new DeviceColumnFilter { PropertyName = "NonExistentProperty", Operator = FilterOperator.String.Equal, Value = "test" }],
      Page = 0,
      PageSize = 10
    };

    var invalidPropertyResult = await controller.SearchDevices(
      invalidPropertyRequest,
      db,
      testApp.App.Services.GetRequiredService<IAuthorizationService>(),
      testApp.App.Services.GetRequiredService<ILogger<DevicesController>>());    // Assert - Invalid filters should be ignored and return all devices
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
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();

    // Create two tenants
    var tenant1 = await testApp.CreateTestTenant("Tenant 1");
    var tenant2 = await testApp.CreateTestTenant("Tenant 2");

    // Create user for tenant 1
    var user1 = await testApp.CreateTestUser(tenant1.Id, email: "user1@example.com", roles: RoleNames.DeviceSuperUser);

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
          TenantId: tenant1.Id,
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
          TenantId: tenant2.Id,
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
    Assert.All(response.Items, device => Assert.Equal(tenant1.Id, device.TenantId));
    Assert.All(response.Items, device => Assert.StartsWith("Tenant1", device.Name));
  }

  [Fact]
  public async Task GetDevicesGridData_ReturnsCorrectDevices()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var controller = testApp.CreateController<DevicesController>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    var logger = testApp.App.Services.GetRequiredService<ILogger<DevicesController>>();
    var authorizationService = testApp.App.Services.GetRequiredService<IAuthorizationService>();

    // Create test tenant and user
    var tenant = await testApp.CreateTestTenant();
    var user = await testApp.CreateTestUser(tenant.Id, roles: RoleNames.DeviceSuperUser);

    // Create test tags
    var tagIds = new Guid[] { Guid.NewGuid(), Guid.NewGuid() }.ToImmutableArray();
    foreach (var tagId in tagIds)
    {
      db.Tags.Add(new Tag { Id = tagId, Name = $"Tag {tagId}", TenantId = tenant.Id });
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
          TenantId: tenant.Id,
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
    };

    var result1 = await controller.SearchDevices(
        request1,
        db,
        authorizationService,
        logger);

    var response1 = result1.Value;

    // Test case 2: Filter by online status
    var request2 = new DeviceSearchRequestDto
    {
      HideOfflineDevices = true,
      Page = 0,
      PageSize = 10
    };

    var result2 = await controller.SearchDevices(
        request2,
        db,
        authorizationService,
        logger);

    var response2 = result2.Value;

    // Test case 3: Filter by tag
    var request3 = new DeviceSearchRequestDto
    {
      TagIds = [tagIds[0]],
      Page = 0,
      PageSize = 10
    };

    var result3 = await controller.SearchDevices(
        request3,
        db,
        authorizationService,
        logger);

    var response3 = result3.Value;

    // Test case 4: Search by name
    var request4 = new DeviceSearchRequestDto
    {
      SearchText = "Device 1",
      Page = 0,
      PageSize = 10
    };

    var result4 = await controller.SearchDevices(
        request4,
        db,
        authorizationService,
        logger);
    var response4 = result4.Value;

    // Test case 5: Sort by CPU utilization (descending)
    var request5 = new DeviceSearchRequestDto
    {
      Page = 0,
      PageSize = 10,
      SortDefinitions = [new DeviceColumnSort { PropertyName = "CpuUtilization", Descending = true, SortOrder = 0 }]
    };

    var result5 = await controller.SearchDevices(
        request5,
        db,
        authorizationService,
        logger);

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
