using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ControlR.Libraries.Shared.Services.Encryption;

namespace ControlR.Web.Server.Tests;

public class DecommissionServerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetDecommissionStatus_WhenDecommissionDisabled_ReturnsFalse()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.Services.CreateScope();

    var controller = scope.CreateController<UserServerSettingsController>();
    var serverOptions = scope.ServiceProvider
      .GetRequiredService<IOptionsMonitor<ServerLifecycleOptions>>();

    var result = controller.GetDecommissionStatus(serverOptions);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<DecommissionServerResponseDto>(okResult.Value);
    Assert.False(dto.IsEnabled);
  }

  [Fact]
  public async Task GetDecommissionStatus_WhenDecommissionEnabled_ReturnsTrue()
  {
    var extraConfig = new Dictionary<string, string?>
    {
      ["ServerLifecycle:DecommissionServer"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      extraConfiguration: extraConfig);
    using var scope = testApp.Services.CreateScope();

    var controller = scope.CreateController<UserServerSettingsController>();
    var serverOptions = scope.ServiceProvider
      .GetRequiredService<IOptionsMonitor<ServerLifecycleOptions>>();

    var result = controller.GetDecommissionStatus(serverOptions);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<DecommissionServerResponseDto>(okResult.Value);
    Assert.True(dto.IsEnabled);
  }

  [Fact]
  public void ServerLifecycleOptions_BindsFromConfiguration()
  {
    var config = new Dictionary<string, string?>
    {
      ["ServerLifecycle:DecommissionServer"] = "true"
    };

    var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
      .AddInMemoryCollection(config)
      .Build();

    var options = configuration
      .GetSection(ServerLifecycleOptions.SectionKey)
      .Get<ServerLifecycleOptions>();

    Assert.NotNull(options);
    Assert.True(options.DecommissionServer);
  }

  [Fact]
  public async Task UpdateDevice_WhenServerDecommissioned_UninstallsAgentAndDeletesDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.Services.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var deviceId = Guid.NewGuid();
    _ = await services.CreateTestDevice(tenant.Id, deviceId);

    var appDb = services.GetRequiredService<AppDb>();
    var timeProvider = services.GetRequiredService<TimeProvider>();
    var appOptions = services.GetRequiredService<IOptions<AppOptions>>();

    // Verify the device exists before the test.
    var preDevice = await appDb.Devices.FindAsync(
      [deviceId],
      TestContext.Current.CancellationToken);
    Assert.NotNull(preDevice);

    // Setup mocks for the AgentHub dependencies that aren't needed in the
    // decommission path but are required by the constructor.
    var mockViewerHub = new Mock<IHubContext<ViewerHub, IViewerHubClient>>();
    var mockDeviceManager = new Mock<IDeviceManager>();
    var mockOutputCache = new Mock<IOutputCacheStore>();
    var mockHubStreamStore = new Mock<IHubStreamStore>();
    var mockAgentVersionProvider = new Mock<IAgentVersionProvider>();
    var mockKeyProvider = new Mock<IEd25519KeyProvider>();
    var mockLogger = new Mock<ILogger<AgentHub>>();

    var serverOptions = Microsoft.Extensions.Options.Options.Create(
      new ServerLifecycleOptions { DecommissionServer = true });

    // Mock SignalR client proxy so we can verify UninstallAgent was called.
    var mockClients = new Mock<IHubCallerClients<IAgentHubClient>>();
    var mockCaller = new Mock<IAgentHubClient>();
    mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);

    var hub = new AgentHub(
      appDb,
      timeProvider,
      mockViewerHub.Object,
      mockDeviceManager.Object,
      mockOutputCache.Object,
      mockHubStreamStore.Object,
      mockAgentVersionProvider.Object,
      appOptions,
      serverOptions,
      mockKeyProvider.Object,
      mockLogger.Object);

    hub.Clients = mockClients.Object;

    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: deviceId,
      Is64Bit: true,
      OsArchitecture: System.Runtime.InteropServices.Architecture.X64,
      Platform: Libraries.Api.Contracts.Enums.SystemPlatform.Windows,
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
      Drives:
      [
        new Drive
        {
          Name = "C:",
          VolumeLabel = "System",
          TotalSize = 256000,
          FreeSpace = 128000
        }
      ]);

    // Act
    var result = await hub.UpdateDevice(deviceDto);

    // Assert - HubResult indicates failure with the decommissioned message.
    Assert.False(result.IsSuccess);
    Assert.Equal("Server is decommissioned.", result.Reason);

    // Assert - Agent was told to uninstall with the correct message.
    mockCaller.Verify(
      x => x.UninstallAgent("Server has been decommissioned."),
      Times.Once);

    // Assert - Device was deleted from the database.
    // Note: In-memory provider does not support ExecuteDeleteAsync.
    // This assertion verifies the database-level deletion logic using a
    // fresh DbContext (not affected by local change tracking).
    using var verifyScope = testApp.Services.CreateScope();
    var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDb>();
    var deviceExists = await verifyDb.Devices
      .AnyAsync(x => x.Id == deviceId, TestContext.Current.CancellationToken);
    Assert.False(deviceExists);
  }

  [Fact]
  public async Task UpdateDevice_WhenServerNotDecommissioned_DoesNotCallUninstallAgent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.Services.CreateScope();
    var services = scope.ServiceProvider;

    var tenant = await services.CreateTestTenant();
    var deviceId = Guid.NewGuid();
    _ = await services.CreateTestDevice(tenant.Id, deviceId);

    var appDb = services.GetRequiredService<AppDb>();
    var timeProvider = services.GetRequiredService<TimeProvider>();
    var appOptions = services.GetRequiredService<IOptions<AppOptions>>();

    // Setup mocks.
    var mockViewerHub = new Mock<IHubContext<ViewerHub, IViewerHubClient>>();
    var mockDeviceManager = new Mock<IDeviceManager>();
    var mockOutputCache = new Mock<IOutputCacheStore>();
    var mockHubStreamStore = new Mock<IHubStreamStore>();
    var mockAgentVersionProvider = new Mock<IAgentVersionProvider>();
    var mockKeyProvider = new Mock<IEd25519KeyProvider>();
    var mockLogger = new Mock<ILogger<AgentHub>>();

    // DecommissionServer is false (the default).
    var serverOptions = Microsoft.Extensions.Options.Options.Create(
      new ServerLifecycleOptions { DecommissionServer = false });

    var mockClients = new Mock<IHubCallerClients<IAgentHubClient>>();
    var mockCaller = new Mock<IAgentHubClient>();
    mockClients.Setup(c => c.Caller).Returns(mockCaller.Object);

    var hub = new AgentHub(
      appDb,
      timeProvider,
      mockViewerHub.Object,
      mockDeviceManager.Object,
      mockOutputCache.Object,
      mockHubStreamStore.Object,
      mockAgentVersionProvider.Object,
      appOptions,
      serverOptions,
      mockKeyProvider.Object,
      mockLogger.Object);

    hub.Clients = mockClients.Object;

    var deviceDto = new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: deviceId,
      Is64Bit: true,
      OsArchitecture: System.Runtime.InteropServices.Architecture.X64,
      Platform: Libraries.Api.Contracts.Enums.SystemPlatform.Windows,
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
      Drives:
      [
        new Drive
        {
          Name = "C:",
          VolumeLabel = "System",
          TotalSize = 256000,
          FreeSpace = 128000
        }
      ]);

    // Act
    var result = await hub.UpdateDevice(deviceDto);

    // Assert - UninstallAgent was NOT called.
    mockCaller.Verify(
      x => x.UninstallAgent(It.IsAny<string>()),
      Times.Never);

    // Assert - Device still exists (was not deleted).
    var device = await appDb.Devices.FindAsync(
      [deviceId],
      TestContext.Current.CancellationToken);
    Assert.NotNull(device);
  }
}
