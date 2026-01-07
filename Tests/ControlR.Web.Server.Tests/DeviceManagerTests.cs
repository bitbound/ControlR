using System.Collections.Immutable;
using System.Net;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class DeviceManagerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task DeviceManager_AddOrUpdate()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var deviceManager = scope.ServiceProvider.GetRequiredService<IDeviceManager>();
    scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var deviceId = Guid.NewGuid();
    var tenantId = Guid.NewGuid();
    var tagIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

    // Create test tags
    foreach (var tagId in tagIds)
    {
      db.Tags.Add(new Tag { Id = tagId, Name = $"Tag {tagId}", TenantId = tenantId });
    }
    await db.SaveChangesAsync();

    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 50,
      Id: deviceId,
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      OsDescription: "Windows 10",
      TenantId: tenantId,
      TotalMemory: 16384,
      TotalStorage: 1024000,
      UsedMemory: 8192,
      UsedStorage: 512000,
      CurrentUsers: ["User1", "User2"],
      MacAddresses: ["00:00:00:00:00:01"],
      LocalIpV4: "10.0.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "Local Disk", TotalSize = 1024000, FreeSpace = 512000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-connection-id",
      RemoteIpAddress: IPAddress.Parse("127.0.0.1"),
      LastSeen: DateTimeOffset.Now,
      IsOnline: true);

    // Act
    var result = await deviceManager.AddOrUpdate(deviceDto, connectionContext, tagIds);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(deviceId, result.Id);
    Assert.Equal("Test Device", result.Name);
    Assert.Equal("1.0.0", result.AgentVersion);
    Assert.Equal(50, result.CpuUtilization);
    Assert.True(result.Is64Bit);
    Assert.True(result.IsOnline);
    Assert.Equal(Architecture.X64, result.OsArchitecture);
    Assert.Equal(SystemPlatform.Windows, result.Platform);
    Assert.Equal(8, result.ProcessorCount);
    Assert.Equal("test-connection-id", result.ConnectionId);
    Assert.Equal("Windows 10", result.OsDescription);
    Assert.Equal(tenantId, result.TenantId);
    Assert.Equal(16384, result.TotalMemory);
    Assert.Equal(1024000, result.TotalStorage);
    Assert.Equal(8192, result.UsedMemory);
    Assert.Equal(512000, result.UsedStorage);
    Assert.Equal(new[] { "User1", "User2" }, result.CurrentUsers);
    Assert.Equal(new[] { "00:00:00:00:00:01" }, result.MacAddresses);
    Assert.Equal("127.0.0.1", result.PublicIpV4);
    Assert.Equal(string.Empty, result.PublicIpV6);
    Assert.Single(result.Drives);
    Assert.Equal("C:", result.Drives[0].Name);

    // Verify tags are associated
    Assert.NotNull(result.Tags);
    Assert.Equal(2, result.Tags.Count);
    Assert.All(result.Tags, tag => Assert.Contains(tag.Id, tagIds));

    // Verify Alias isn't updated (DeviceDto.Alias shouldn't update the entity)
    Assert.Equal(string.Empty, result.Alias);

    // Test update of existing device
    var updatedDto = deviceDto with
    {
      Name = "Updated Device",
      AgentVersion = "1.0.1",
      OsDescription = "Windows 11"
    };

    var updatedResult = await deviceManager.AddOrUpdate(updatedDto, connectionContext, tagIds: null);

    Assert.NotNull(updatedResult);
    Assert.Equal(deviceId, updatedResult.Id);
    Assert.Equal("Updated Device", updatedResult.Name);
    Assert.Equal("1.0.1", updatedResult.AgentVersion);
    Assert.Equal("Windows 11", updatedResult.OsDescription);

    // Verify tags aren't updated since tagIds is null
    Assert.NotNull(updatedResult.Tags);
    Assert.Equal(2, updatedResult.Tags.Count);
  }

  [Fact]
  public async Task DeviceManager_CanInstallAgentOnDevice()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var deviceManager = scope.ServiceProvider.GetRequiredService<IDeviceManager>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var tenantId = Guid.NewGuid();
    var otherTenantId = Guid.NewGuid();

    // Create a test device
    var device = new Device
    {
      Id = Guid.NewGuid(),
      Name = "Test Device",
      TenantId = tenantId
    };
    db.Devices.Add(device);

    // Create users with different permissions
    var installerUser = new AppUser
    {
      Id = Guid.NewGuid(),
      UserName = "installer@example.com",
      NormalizedUserName = "INSTALLER@EXAMPLE.COM",
      Email = "installer@example.com",
      NormalizedEmail = "INSTALLER@EXAMPLE.COM",
      EmailConfirmed = true,
      TenantId = tenantId
    };

    var installerUserResult = await userManager.CreateAsync(installerUser);
    Assert.True(installerUserResult.Succeeded);

    var nonInstallerUser = new AppUser
    {
      Id = Guid.NewGuid(),
      UserName = "regular@example.com",
      NormalizedUserName = "REGULAR@EXAMPLE.COM",
      Email = "regular@example.com",
      NormalizedEmail = "REGULAR@EXAMPLE.COM",
      EmailConfirmed = true,
      TenantId = tenantId
    };

    var nonInstallerUserResult = await userManager.CreateAsync(nonInstallerUser);
    Assert.True(nonInstallerUserResult.Succeeded);

    var differentTenantUser = new AppUser
    {
      Id = Guid.NewGuid(),
      UserName = "different@example.com",
      NormalizedUserName = "DIFFERENT@EXAMPLE.COM",
      Email = "different@example.com",
      NormalizedEmail = "DIFFERENT@EXAMPLE.COM",
      EmailConfirmed = true,
      TenantId = otherTenantId
    };

    var differentTenantUserResult = await userManager.CreateAsync(differentTenantUser);
    Assert.True(differentTenantUserResult.Succeeded);

    await db.SaveChangesAsync();

    await userManager.AddToRoleAsync(installerUser, RoleNames.AgentInstaller);
    await userManager.AddToRoleAsync(differentTenantUser, RoleNames.AgentInstaller);

    // Act & Assert

    // Installer user from same tenant should be able to install
    var canInstall = await deviceManager.CanInstallAgentOnDevice(installerUser, device);
    Assert.True(canInstall);

    // Non-installer user from same tenant should not be able to install
    var canNonInstallerInstall = await deviceManager.CanInstallAgentOnDevice(nonInstallerUser, device);
    Assert.False(canNonInstallerInstall);

    // User from different tenant should not be able to install
    var canDifferentTenantInstall = await deviceManager.CanInstallAgentOnDevice(differentTenantUser, device);
    Assert.False(canDifferentTenantInstall);
  }

  [Fact]
  public async Task DeviceManager_UpdateDevice()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.App.Services.CreateScope();
    var deviceManager = scope.ServiceProvider.GetRequiredService<IDeviceManager>();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var deviceId = Guid.NewGuid();
    var tenantId = Guid.NewGuid();
    var tagIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

    // Create test tags
    foreach (var tagId in tagIds)
    {
      db.Tags.Add(new Tag { Id = tagId, Name = $"Tag {tagId}", TenantId = tenantId });
    }

    // Create a device in the database first
    var device = new Device
    {
      Id = deviceId,
      Name = "Original Device",
      AgentVersion = "1.0.0",
      TenantId = tenantId,
      Alias = "Original Alias"
    };
    db.Devices.Add(device);
    await db.SaveChangesAsync();

    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Updated Device",
      AgentVersion: "2.0.0",
      CpuUtilization: 75,
      Id: deviceId,
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      OsDescription: "Windows 11",
      TenantId: tenantId,
      TotalMemory: 32768,
      TotalStorage: 2048000,
      UsedMemory: 16384,
      UsedStorage: 1024000,
      CurrentUsers: ["User1"],
      MacAddresses: ["00:00:00:00:00:02"],
      LocalIpV4: "192.168.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 2048000, FreeSpace = 1024000 }]);

    var connectionContext = new DeviceConnectionContext(
      ConnectionId: "test-connection-id",
      RemoteIpAddress: IPAddress.Parse("192.168.1.1"),
      LastSeen: DateTimeOffset.Now,
      IsOnline: true);

    // Act
    var result = await deviceManager.UpdateDevice(deviceDto, connectionContext, tagIds);

    // Assert
    Assert.True(result.IsSuccess);
    var updatedDevice = result.Value;

    Assert.NotNull(updatedDevice);
    Assert.Equal(deviceId, updatedDevice.Id);
    Assert.Equal("Updated Device", updatedDevice.Name);
    Assert.Equal("2.0.0", updatedDevice.AgentVersion);
    Assert.Equal(75, updatedDevice.CpuUtilization);
    Assert.True(updatedDevice.Is64Bit);
    Assert.True(updatedDevice.IsOnline);
    Assert.Equal(Architecture.X64, updatedDevice.OsArchitecture);
    Assert.Equal(SystemPlatform.Windows, updatedDevice.Platform);
    Assert.Equal(8, updatedDevice.ProcessorCount);
    Assert.Equal("test-connection-id", updatedDevice.ConnectionId);
    Assert.Equal("Windows 11", updatedDevice.OsDescription);
    Assert.Equal(tenantId, updatedDevice.TenantId);

    // Verify tags are associated
    Assert.NotNull(updatedDevice.Tags);
    Assert.Equal(2, updatedDevice.Tags.Count);
    Assert.All(updatedDevice.Tags, tag => Assert.Contains(tag.Id, tagIds));

    // Verify Alias isn't updated (DeviceDto.Alias shouldn't update the entity)
    Assert.Equal("Original Alias", updatedDevice.Alias);

    // Test update with non-existent device ID
    var nonExistentDto = deviceDto with { Id = Guid.NewGuid() };
    var failResult = await deviceManager.UpdateDevice(nonExistentDto, connectionContext);
    Assert.False(failResult.IsSuccess);
    Assert.Equal("Device does not exist in the database.", failResult.Reason);
  }
}