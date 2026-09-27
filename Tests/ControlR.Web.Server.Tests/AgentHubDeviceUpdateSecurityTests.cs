using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.DeviceManagement;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins the per-method trust boundary on <see cref="AgentHub"/>. The hub is intentionally
/// anonymous, so these checks stop a keyless or unenrolled device from being adopted.
/// </summary>
public class AgentHubDeviceUpdateSecurityTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task UpdateDeviceSigned_ExistingKeylessDevice_IsRejected()
  {
    await using var fixture = await HubFixture.Create(_testOutput, allowSelfBootstrap: false);

    var deviceId = Guid.NewGuid();
    _ = await fixture.Services.CreateTestDevice(fixture.TenantId, deviceId);

    var keyPair = fixture.KeyProvider.GenerateKeyPair();
    var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);
    var signedDto = fixture.KeyProvider.Sign(
      CreateDeviceDto(deviceId, fixture.TenantId),
      keyPair.PrivateKey,
      publicKeyBase64);

    var result = await fixture.Hub.UpdateDeviceSigned(signedDto);

    Assert.False(result.IsSuccess);
    Assert.Equal("Device requires enrollment.", result.Reason);

    var device = await fixture.AppDb.Devices.FindAsync(
      [deviceId],
      TestContext.Current.CancellationToken);
    Assert.NotNull(device);
    Assert.Empty(device.PublicKey);
  }

  [Fact]
  public async Task UpdateDeviceSigned_KnownDeviceWithSelfBootstrapDisabled_UsesServerTenant()
  {
    await using var fixture = await HubFixture.Create(_testOutput, allowSelfBootstrap: false);

    var deviceId = Guid.NewGuid();
    var keyPair = fixture.KeyProvider.GenerateKeyPair();
    var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);
    _ = await fixture.Services.CreateTestDevice(fixture.TenantId, deviceId, publicKeyBase64);

    // The caller names a tenant that does not exist. The server must ignore it and use the
    // tenant the device is already enrolled under.
    var signedDto = fixture.KeyProvider.Sign(
      CreateDeviceDto(deviceId, Guid.NewGuid()),
      keyPair.PrivateKey,
      publicKeyBase64);

    var result = await fixture.Hub.UpdateDeviceSigned(signedDto);

    Assert.True(result.IsSuccess, result.Reason);

    var device = await fixture.AppDb.Devices
      .IgnoreQueryFilters()
      .FirstAsync(x => x.Id == deviceId, TestContext.Current.CancellationToken);
    Assert.Equal(fixture.TenantId, device.TenantId);
  }

  [Fact]
  public async Task UpdateDeviceSigned_KnownDevice_DoesNotReplaceStoredKey()
  {
    await using var fixture = await HubFixture.Create(_testOutput, allowSelfBootstrap: true);

    var deviceId = Guid.NewGuid();
    var keyPair = fixture.KeyProvider.GenerateKeyPair();
    var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);
    _ = await fixture.Services.CreateTestDevice(fixture.TenantId, deviceId, publicKeyBase64);

    var signedDto = fixture.KeyProvider.Sign(
      CreateDeviceDto(deviceId, fixture.TenantId),
      keyPair.PrivateKey,
      publicKeyBase64);

    var result = await fixture.Hub.UpdateDeviceSigned(signedDto);

    Assert.True(result.IsSuccess, result.Reason);

    var device = await fixture.AppDb.Devices
      .IgnoreQueryFilters()
      .FirstAsync(x => x.Id == deviceId, TestContext.Current.CancellationToken);
    Assert.Equal(publicKeyBase64, device.PublicKey);
  }

  [Fact]
  public async Task UpdateDeviceSigned_SelfBootstrapOnMultiTenantServer_NamingAnExistingTenant_IsRejected()
  {
    await using var fixture = await HubFixture.Create(_testOutput, allowSelfBootstrap: true);

    // A second tenant makes the server multi-tenant, so naming the first tenant must not
    // bypass the single-tenant guard.
    _ = await fixture.Services.CreateTestTenant("Second Tenant");

    var deviceId = Guid.NewGuid();
    var keyPair = fixture.KeyProvider.GenerateKeyPair();
    var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);
    var signedDto = fixture.KeyProvider.Sign(
      CreateDeviceDto(deviceId, fixture.TenantId),
      keyPair.PrivateKey,
      publicKeyBase64);

    var result = await fixture.Hub.UpdateDeviceSigned(signedDto);

    Assert.False(result.IsSuccess);
    Assert.Equal(
      "Self-bootstrap is only allowed on single-tenant servers. Use an installer key instead.",
      result.Reason);

    var exists = await fixture.AppDb.Devices
      .IgnoreQueryFilters()
      .AnyAsync(x => x.Id == deviceId, TestContext.Current.CancellationToken);
    Assert.False(exists);
  }

  [Fact]
  public async Task UpdateDeviceSigned_SelfBootstrapOnSingleTenantServer_CreatesDevice()
  {
    await using var fixture = await HubFixture.Create(_testOutput, allowSelfBootstrap: true);

    var deviceId = Guid.NewGuid();
    var keyPair = fixture.KeyProvider.GenerateKeyPair();
    var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);

    // The caller names a bogus tenant. The server must supply the real one.
    var signedDto = fixture.KeyProvider.Sign(
      CreateDeviceDto(deviceId, Guid.NewGuid()),
      keyPair.PrivateKey,
      publicKeyBase64);

    var result = await fixture.Hub.UpdateDeviceSigned(signedDto);

    Assert.True(result.IsSuccess, result.Reason);

    var device = await fixture.AppDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceId, TestContext.Current.CancellationToken);
    Assert.NotNull(device);
    Assert.Equal(fixture.TenantId, device.TenantId);
    Assert.Equal(publicKeyBase64, device.PublicKey);
  }

  [Fact]
  public async Task UpdateDeviceSigned_UnknownDeviceWithSelfBootstrapDisabled_IsRejected()
  {
    await using var fixture = await HubFixture.Create(_testOutput, allowSelfBootstrap: false);

    var deviceId = Guid.NewGuid();
    var keyPair = fixture.KeyProvider.GenerateKeyPair();
    var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);
    var signedDto = fixture.KeyProvider.Sign(
      CreateDeviceDto(deviceId, fixture.TenantId),
      keyPair.PrivateKey,
      publicKeyBase64);

    var result = await fixture.Hub.UpdateDeviceSigned(signedDto);

    Assert.False(result.IsSuccess);
    Assert.Equal("Unknown device.", result.Reason);

    var exists = await fixture.AppDb.Devices
      .IgnoreQueryFilters()
      .AnyAsync(x => x.Id == deviceId, TestContext.Current.CancellationToken);
    Assert.False(exists);
  }

  [Fact]
  public async Task UpdateDevice_IsNoLongerExposedOnTheHubContract()
  {
    var method = typeof(AgentHub).GetMethod(
      "UpdateDevice",
      [typeof(DeviceUpdateRequestDto)]);

    Assert.Null(method);
  }

  private static DeviceUpdateRequestDto CreateDeviceDto(Guid deviceId, Guid tenantId)
  {
    return new DeviceUpdateRequestDto(
      Name: "Test Device",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: deviceId,
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
  }

  private sealed class HubFixture : IAsyncDisposable
  {
    private readonly IServiceScope _scope;
    private readonly TestApp _testApp;

    private HubFixture(TestApp testApp, IServiceScope scope, AgentHub hub, Guid tenantId)
    {
      _testApp = testApp;
      _scope = scope;
      Hub = hub;
      TenantId = tenantId;
    }

    public AppDb AppDb { get; private init; } = default!;
    public AgentHub Hub { get; }
    public Ed25519KeyProvider KeyProvider { get; private init; } = default!;
    public IServiceProvider Services => _scope.ServiceProvider;
    public Guid TenantId { get; }

    public static async Task<HubFixture> Create(
      ITestOutputHelper testOutput,
      bool allowSelfBootstrap)
    {
      var extraConfig = new Dictionary<string, string?>
      {
        ["DeveloperOptions:AllowAgentsToSelfBootstrap"] = allowSelfBootstrap.ToString()
      };

      var testApp = await TestAppBuilder.CreateTestApp(
        testOutput,
        extraConfiguration: extraConfig);
      var scope = testApp.Services.CreateScope();
      var services = scope.ServiceProvider;

      var tenant = await services.CreateTestTenant();
      var appDb = services.GetRequiredService<AppDb>();
      var keyProvider = new Ed25519KeyProvider(
        services.GetRequiredService<TimeProvider>(),
        NullLogger<Ed25519KeyProvider>.Instance);

      var viewerHub = new Mock<IHubContext<ViewerHub, IViewerHubClient>>();
      var viewerClients = new Mock<IHubClients<IViewerHubClient>>();
      viewerClients
        .Setup(x => x.Group(It.IsAny<string>()))
        .Returns(new Mock<IViewerHubClient>().Object);
      viewerHub.SetupGet(x => x.Clients).Returns(viewerClients.Object);
      var outputCache = new Mock<IOutputCacheStore>();
      var hubStreamStore = new Mock<IHubStreamStore>();
      var agentVersionProvider = new Mock<IAgentVersionProvider>();
      agentVersionProvider
        .Setup(x => x.TryGetAgentVersion(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HttpResult<Version>(new Version(0, 0, 0)));

      var hub = new AgentHub(
        appDb,
        services.GetRequiredService<TimeProvider>(),
        viewerHub.Object,
        services.GetRequiredService<IDeviceManager>(),
        outputCache.Object,
        hubStreamStore.Object,
        agentVersionProvider.Object,
        services.GetRequiredService<IOptions<AppOptions>>(),
        Microsoft.Extensions.Options.Options.Create(new DeveloperOptions
        {
          AllowAgentsToSelfBootstrap = allowSelfBootstrap
        }),
        services.GetRequiredService<IOptions<ServerLifecycleOptions>>(),
        keyProvider,
        services.GetRequiredService<ILogger<AgentHub>>());
      hub.Context = new TestHubCallerContext();
      hub.Clients = new Mock<IHubCallerClients<IAgentHubClient>>().Object;

      return new HubFixture(testApp, scope, hub, tenant.Id)
      {
        AppDb = appDb,
        KeyProvider = keyProvider
      };
    }

    public async ValueTask DisposeAsync()
    {
      _scope.Dispose();
      await _testApp.DisposeAsync();
    }
  }
  private sealed class TestHubCallerContext : HubCallerContext
  {
    private readonly CancellationTokenSource _connectionAborted = new();

    public override CancellationToken ConnectionAborted => _connectionAborted.Token;
    public override string ConnectionId { get; } = Guid.NewGuid().ToString();
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override IDictionary<object, object?> Items { get; } =
      new Dictionary<object, object?>();
    public override ClaimsPrincipal User { get; } = new();
    public override string? UserIdentifier => null;

    public override void Abort()
    {
      _connectionAborted.Cancel();
    }
  }
}
