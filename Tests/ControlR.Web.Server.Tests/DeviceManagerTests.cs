using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using System.Collections.Immutable;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using ControlR.Web.Client.Authz;

namespace ControlR.Web.Server.Tests;

public class DeviceManagerTests(ITestOutputHelper testOutput)
{

  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task DeviceManager_AddOrUpdate()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceId = Guid.NewGuid();
    var tenantId = Guid.NewGuid();
    var tagIds = new Guid[] { Guid.NewGuid(), Guid.NewGuid() }.ToImmutableArray();

    // Create test tags
    foreach (var tagId in tagIds)
    {
      db.Tags.Add(new Tag { Id = tagId, Name = $"Tag {tagId}", TenantId = tenantId });
    }
    await db.SaveChangesAsync();

    var deviceDto = new DeviceDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 50,
      Id: deviceId,
      Is64Bit: true,
      IsOnline: true,
      LastSeen: DateTimeOffset.Now,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      ConnectionId: "test-connection-id",
      OsDescription: "Windows 10",
      TenantId: tenantId,
      TotalMemory: 16384,
      TotalStorage: 1024000,
      UsedMemory: 8192,
      UsedStorage: 512000,
      CurrentUsers: ["User1", "User2"],
      MacAddresses: ["00:00:00:00:00:01"], PublicIpV4: "127.0.0.1",
      PublicIpV6: "::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "Local Disk", TotalSize = 1024000, FreeSpace = 512000 }])
    {
      TagIds = tagIds,
      Alias = "Test Alias"
    };

    // Act
    var result = await deviceManager.AddOrUpdate(deviceDto, addTagIds: true);

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
    Assert.Equal("::1", result.PublicIpV6);
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

    var updatedResult = await deviceManager.AddOrUpdate(updatedDto, addTagIds: false);

    Assert.NotNull(updatedResult);
    Assert.Equal(deviceId, updatedResult.Id);
    Assert.Equal("Updated Device", updatedResult.Name);
    Assert.Equal("1.0.1", updatedResult.AgentVersion);
    Assert.Equal("Windows 11", updatedResult.OsDescription);

    // Verify tags aren't updated since addTagIds is false
    Assert.NotNull(updatedResult.Tags);
    Assert.Equal(2, updatedResult.Tags.Count);
  }

  [Fact]
  public async Task DeviceManager_UpdateDevice()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

    var deviceId = Guid.NewGuid();
    var tenantId = Guid.NewGuid();
    var tagIds = new Guid[] { Guid.NewGuid(), Guid.NewGuid() }.ToImmutableArray();

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

    var deviceDto = new DeviceDto(
      Name: "Updated Device",
      AgentVersion: "2.0.0",
      CpuUtilization: 75,
      Id: deviceId,
      Is64Bit: true,
      IsOnline: true,
      LastSeen: DateTimeOffset.Now,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      ConnectionId: "test-connection-id",
      OsDescription: "Windows 11",
      TenantId: tenantId,
      TotalMemory: 32768,
      TotalStorage: 2048000,
      UsedMemory: 16384,
      UsedStorage: 1024000,
      CurrentUsers: ["User1"],
      MacAddresses: ["00:00:00:00:00:02"],
      PublicIpV4: "192.168.1.1",
      PublicIpV6: "::2",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 2048000, FreeSpace = 1024000 }])
    {
      TagIds = tagIds,
      Alias = "New Alias" // Should be ignored based on the requirement
    };

    // Act
    var result = await deviceManager.UpdateDevice(deviceDto, addTagIds: true);

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
    var failResult = await deviceManager.UpdateDevice(nonExistentDto);
    Assert.False(failResult.IsSuccess);
    Assert.Equal("Device does not exist in the database.", failResult.Reason);
  }

  [Fact]
  public async Task DeviceManager_CanInstallAgentOnDevice()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    var deviceManager = testApp.App.Services.GetRequiredService<IDeviceManager>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    using var db = testApp.App.Services.GetRequiredService<AppDb>();

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

    // Create a role for agent installer
    var installerRole = new AppRole
    {
      Id = Guid.NewGuid(),
      Name = RoleNames.AgentInstaller,
      NormalizedName = RoleNames.AgentInstaller.ToUpper()
    };
    await db.Roles.AddAsync(installerRole);

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

    await db.Users.AddRangeAsync(installerUser, nonInstallerUser, differentTenantUser);

    // Assign installer role to the installer user
    await db.UserRoles.AddAsync(new IdentityUserRole<Guid>
    {
      UserId = installerUser.Id,
      RoleId = installerRole.Id
    });

    await db.SaveChangesAsync();

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
}