using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Constants;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class DevicesControllerCreateDeviceTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateDevice_ConcurrentRequests_WithSingleUseKey_OnlyOneSucceeds()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(
      tenantId: tenant.Id,
      email: "test@example.com",
      roles: [RoleNames.DeviceSuperUser, RoleNames.AgentInstaller]);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.UsageBased,
      allowedUses: 1,
      expiration: null,
      friendlyName: "Single Use Key");

    var tasks = Enumerable.Range(0, 5).Select(async _ =>
    {
      using var client = testServer.TestServer.CreateClient();
      var deviceDto = CreateDeviceDto(tenant.Id);
      var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);
      return await client.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);
    });

    var responses = await Task.WhenAll(tasks);
    var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
    var failureCount = responses.Count(r => !r.IsSuccessStatusCode);

    Assert.Equal(1, successCount);
    Assert.Equal(4, failureCount);
  }
  [Fact]
  public async Task CreateDevice_ExistingDevice_UpdatesDeviceProperties()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(
      tenantId: tenant.Id,
      email: "test@example.com",
      roles: [RoleNames.DeviceSuperUser, RoleNames.AgentInstaller]);

    var existingDevice = await services.CreateTestDevice(tenant.Id);
    var originalName = existingDevice.Name;

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    var updatedDeviceDto = CreateDeviceDto(tenant.Id, deviceId: existingDevice.Id);
    var requestDto = new CreateDeviceRequestDto(updatedDeviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var dbDevice = await db.Devices.FirstOrDefaultAsync(d => d.Id == existingDevice.Id);
    Assert.NotNull(dbDevice);
    Assert.NotEqual(originalName, dbDevice.Name);
    Assert.Equal(updatedDeviceDto.Name, dbDevice.Name);
  }
  [Theory]
  [InlineData(SystemPlatform.Windows)]
  [InlineData(SystemPlatform.Linux)]
  [InlineData(SystemPlatform.MacOs)]
  public async Task CreateDevice_ExistingDevice_WithAuthorizedUser_Succeeds(SystemPlatform platform)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(
      tenantId: tenant.Id,
      email: "authorized@example.com",
      roles: [RoleNames.DeviceSuperUser, RoleNames.AgentInstaller]);

    var existingDevice = await services.CreateTestDevice(tenant.Id);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    var deviceDto = CreateDeviceDto(tenant.Id, deviceId: existingDevice.Id, platform: platform);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_ExistingDevice_WithDeletedKeyCreator_Fails()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(
      tenantId: tenant.Id,
      email: "test@example.com",
      roles: [RoleNames.DeviceSuperUser, RoleNames.AgentInstaller]);

    var existingDevice = await services.CreateTestDevice(tenant.Id);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    using (var scope = services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var userEntity = await db.Users.FirstAsync(u => u.Id == user.Id);
      db.Users.Remove(userEntity);
      await db.SaveChangesAsync();
    }

    var deviceDto = CreateDeviceDto(tenant.Id, deviceId: existingDevice.Id);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_ExistingDevice_WithUnauthorizedUser_Fails()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var deviceOwner = await services.CreateTestUser(
      tenantId: tenant.Id,
      email: "owner@test.com",
      roles: [RoleNames.DeviceSuperUser, RoleNames.AgentInstaller]);
    var unauthorizedUser = await services.CreateTestUser(tenant.Id, "unauthorized@test.com");

    var existingDevice = await services.CreateTestDevice(tenant.Id);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: unauthorizedUser.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Unauthorized Key");

    var deviceDto = CreateDeviceDto(tenant.Id, deviceId: existingDevice.Id);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_MultipleDevices_WithUsageBasedKey_TracksUsagesCorrectly()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.UsageBased,
      allowedUses: 5,
      expiration: null,
      friendlyName: "Multi-Use Key");

    var deviceIds = new List<Guid>();

    for (var i = 0; i < 5; i++)
    {
      var deviceDto = CreateDeviceDto(tenant.Id);
      deviceIds.Add(deviceDto.Id);
      var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

      var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    var deviceDto6 = CreateDeviceDto(tenant.Id);
    var requestDto6 = new CreateDeviceRequestDto(deviceDto6, installerKey.Id, installerKey.KeySecret, TagIds: null);
    var response6 = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto6);

    Assert.Equal(HttpStatusCode.BadRequest, response6.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_NewDevice_SetsIsOnlineToFalse()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    var deviceDto = CreateDeviceDto(tenant.Id);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resultDevice = await response.Content.ReadFromJsonAsync<DeviceResponseDto>();
    Assert.NotNull(resultDevice);
    Assert.False(resultDevice.IsOnline);

    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var dbDevice = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceDto.Id);
    Assert.NotNull(dbDevice);
    Assert.False(dbDevice.IsOnline);
  }
  [Fact]
  public async Task CreateDevice_NewDevice_WithEmptyDeviceId_Fails()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    var deviceDto = CreateDeviceDto(tenant.Id, deviceId: Guid.Empty);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_NewDevice_WithExhaustedUsageBasedKey_Fails()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.UsageBased,
      allowedUses: 1,
      expiration: null,
      friendlyName: "Single Use Key");

    var deviceDto1 = CreateDeviceDto(tenant.Id);
    var requestDto1 = new CreateDeviceRequestDto(deviceDto1, installerKey.Id, installerKey.KeySecret, TagIds: null);
    var response1 = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto1);
    Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

    var deviceDto2 = CreateDeviceDto(tenant.Id);
    var requestDto2 = new CreateDeviceRequestDto(deviceDto2, installerKey.Id, installerKey.KeySecret, TagIds: null);
    var response2 = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto2);

    Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_NewDevice_WithExpiredTimeBasedKey_Fails()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.TimeBased,
      allowedUses: null,
      expiration: testServer.TimeProvider.GetUtcNow().AddHours(1),
      friendlyName: "Time Based Key");

    testServer.TimeProvider.Advance(TimeSpan.FromHours(2));

    var deviceDto = CreateDeviceDto(tenant.Id);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_NewDevice_WithInvalidKeySecret_Fails()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    var deviceDto = CreateDeviceDto(tenant.Id);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, "wrong-secret", TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
  [Fact]
  public async Task CreateDevice_NewDevice_WithNonExistentKeyId_Fails()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();

    var deviceDto = CreateDeviceDto(tenant.Id);
    var requestDto = new CreateDeviceRequestDto(deviceDto, Guid.NewGuid(), "some-secret", TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
  [Theory]
  [InlineData(SystemPlatform.Windows)]
  [InlineData(SystemPlatform.Linux)]
  [InlineData(SystemPlatform.MacOs)]
  public async Task CreateDevice_NewDevice_WithPersistentKey_Succeeds(SystemPlatform platform)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Persistent Key");

    var deviceDto = CreateDeviceDto(tenant.Id, platform: platform);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resultDevice = await response.Content.ReadFromJsonAsync<DeviceResponseDto>();
    Assert.NotNull(resultDevice);
    Assert.Equal(deviceDto.Id, resultDevice.Id);
    Assert.Equal(deviceDto.Name, resultDevice.Name);
    Assert.Equal(platform, resultDevice.Platform);
    Assert.False(resultDevice.IsOnline);
  }
  [Theory]
  [InlineData(SystemPlatform.Windows)]
  [InlineData(SystemPlatform.Linux)]
  [InlineData(SystemPlatform.MacOs)]
  public async Task CreateDevice_NewDevice_WithSingleUseKey_KeyIsRemovedAfterUse(SystemPlatform platform)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.UsageBased,
      allowedUses: 1,
      expiration: null,
      friendlyName: "Single Use Key");

    var deviceDto = CreateDeviceDto(tenant.Id, platform: platform);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var key = await db.AgentInstallerKeys.FirstOrDefaultAsync(k => k.Id == installerKey.Id);

    Assert.Null(key);
  }
  [Theory]
  [InlineData(SystemPlatform.Windows)]
  [InlineData(SystemPlatform.Linux)]
  [InlineData(SystemPlatform.MacOs)]
  public async Task CreateDevice_NewDevice_WithTags_TagsAreApplied(SystemPlatform platform)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Tag1", TenantId = tenant.Id };
    var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Tag2", TenantId = tenant.Id };
    db.Tags.AddRange(tag1, tag2);
    await db.SaveChangesAsync();

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Test Key");

    var deviceDto = CreateDeviceDto(tenant.Id, platform: platform);
    var tags = new[] { tag1.Id, tag2.Id };
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: tags);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var createdDevice = await db.Devices
      .Include(d => d.Tags)
      .FirstOrDefaultAsync(d => d.Id == deviceDto.Id);

    Assert.NotNull(createdDevice);
    Assert.NotNull(createdDevice.Tags);
    Assert.Equal(2, createdDevice.Tags.Count);
    Assert.Contains(createdDevice.Tags, t => t.Id == tag1.Id);
    Assert.Contains(createdDevice.Tags, t => t.Id == tag2.Id);
  }
  [Theory]
  [InlineData(SystemPlatform.Windows)]
  [InlineData(SystemPlatform.Linux)]
  [InlineData(SystemPlatform.MacOs)]
  public async Task CreateDevice_NewDevice_WithTimeBasedKey_Succeeds(SystemPlatform platform)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.TimeBased,
      allowedUses: null,
      expiration: testServer.TimeProvider.GetUtcNow().AddHours(1),
      friendlyName: "Time Based Key");

    var deviceDto = CreateDeviceDto(tenant.Id, platform: platform);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }
  [Theory]
  [InlineData(SystemPlatform.Windows)]
  [InlineData(SystemPlatform.Linux)]
  [InlineData(SystemPlatform.MacOs)]
  public async Task CreateDevice_NewDevice_WithUsageBasedKey_ConsumesOneUsage(SystemPlatform platform)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.UsageBased,
      allowedUses: 3,
      expiration: null,
      friendlyName: "Test Usage Key");

    var deviceDto = CreateDeviceDto(tenant.Id, platform: platform);
    var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

    var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var key = await db.AgentInstallerKeys
      .Include(k => k.Usages)
      .FirstOrDefaultAsync(k => k.Id == installerKey.Id);

    Assert.NotNull(key);
    Assert.Single(key.Usages);
    Assert.Equal(deviceDto.Id, key.Usages.First().DeviceId);
  }
  [Fact]
  public async Task CreateDevice_PersistentKey_CanBeUsedMultipleTimes()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, roles: RoleNames.TenantAdministrator);

    var keyManager = services.GetRequiredService<IAgentInstallerKeyManager>();
    var installerKey = await keyManager.CreateKey(
      tenantId: tenant.Id,
      creatorId: user.Id,
      keyType: InstallerKeyType.Persistent,
      allowedUses: null,
      expiration: null,
      friendlyName: "Persistent Key");

    for (var i = 0; i < 10; i++)
    {
      var deviceDto = CreateDeviceDto(tenant.Id);
      var requestDto = new CreateDeviceRequestDto(deviceDto, installerKey.Id, installerKey.KeySecret, TagIds: null);

      var response = await httpClient.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var key = await db.AgentInstallerKeys
      .Include(k => k.Usages)
      .FirstOrDefaultAsync(k => k.Id == installerKey.Id);

    Assert.NotNull(key);
    Assert.Equal(10, key.Usages.Count);
  }

  private static DeviceConnectionContext CreateConnectionContext(Guid? deviceId = null)
  {
    var id = deviceId ?? Guid.NewGuid();
    return new DeviceConnectionContext(
     ConnectionId: $"test-connection-{id}",
     RemoteIpAddress: IPAddress.Parse("192.168.1.100"),
     LastSeen: DateTimeOffset.UtcNow,
     IsOnline: true
    );
  }
  private static DeviceUpdateRequestDto CreateDeviceDto(
    Guid tenantId,
    Guid? deviceId = null,
    SystemPlatform platform = SystemPlatform.Windows)
  {
    var id = deviceId ?? Guid.NewGuid();
    return new DeviceUpdateRequestDto(
      Name: $"Test Device {id}",
      AgentVersion: "1.0.0",
      CpuUtilization: 25.0,
      Id: id,
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      Platform: platform,
      ProcessorCount: 8,
      OsDescription: GetOsDescription(platform),
      TenantId: tenantId,
      TotalMemory: 16384,
      TotalStorage: 512000,
      UsedMemory: 8192,
      UsedStorage: 256000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:11:22:33:44:55"],
      LocalIpV4: "10.0.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", VolumeLabel = "System", TotalSize = 512000, FreeSpace = 256000 }]
    );
  }
  private static string GetOsDescription(SystemPlatform platform)
  {
    return platform switch
    {
      SystemPlatform.Windows => "Windows 11 Pro",
      SystemPlatform.Linux => "Ubuntu 22.04 LTS",
      SystemPlatform.MacOs => "macOS 14.0 Sonoma",
      _ => "Unknown OS"
    };
  }
}
