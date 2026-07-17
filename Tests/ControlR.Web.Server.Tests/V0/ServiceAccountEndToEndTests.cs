using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountEndToEndTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task ServiceAccount_EndToEnd_CompletesFullProvisioningFlow()
  {
    // Step 1: Turn self-registration off and bootstrap a service account on startup.
    var bootstrapAccountId = Guid.NewGuid();
    var bootstrapTokenId = Guid.NewGuid();
    const string bootstrapSecret = "a-very-strong-bootstrap-secret-key-32-long";
    const string saName = "bootstrap-sa";

    var config = new Dictionary<string, string?>
    {
      ["AppOptions:DisableEmailSending"] = "true",
      ["AppOptions:EnableFirstUserSelfRegistration"] = "false",
      ["Bootstrap:ServerServiceAccountId"] = $"{bootstrapAccountId}",
      ["Bootstrap:ServerServiceAccountName"] = saName,
      ["Bootstrap:ServerServiceAccountTokenId"] = $"{bootstrapTokenId}",
      ["Bootstrap:ServerServiceAccountTokenSecret"] = bootstrapSecret,
    };

    var fakeDesktopSessions = CreateFakeDesktopSessions();

    var mockAgentHubContext = CreateMockAgentHubContext(fakeDesktopSessions);

    // Bootstrap will happen when the server starts.
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: config,
      configureServices: services =>
      {
        services.AddSingleton(mockAgentHubContext.Object);
      });

    var services = testServer.Services;
    var saManager = services.GetRequiredService<IServiceAccountManager>();

    // Verify the bootstrapped account exists.
    var saAccounts = await saManager.GetAllForServer(TestContext.Current.CancellationToken);
    var sa = saAccounts.First(s => s.Name == saName);
    Assert.Equal(bootstrapAccountId, sa.Id);

    var apiKey = $"{Convert.ToHexString(bootstrapTokenId.ToByteArray())}:{bootstrapSecret}";

    // Step 2: Service account creates a tenant.
    using var saClient = await testServer.GetHttpClient();

    saClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      apiKey);

    var createTenantReq = new V0Dtos.CreateTenantRequestDto("EndToEnd Tenant");
    var tenantResponse = await saClient.PostAsJsonAsync(
      HttpConstants.V0.TenantsEndpoint,
      createTenantReq,
      TestContext.Current.CancellationToken);

    Assert.True(tenantResponse.IsSuccessStatusCode,
      $"Create tenant failed: {tenantResponse.StatusCode}");

    var tenantResult = await tenantResponse.Content
      .ReadFromJsonAsync<V0Dtos.CreateTenantResponseDto>(TestContext.Current.CancellationToken);

    Assert.NotNull(tenantResult);
    Assert.NotEqual(Guid.Empty, tenantResult.TenantId);

    // Step 3: Service account creates an installer key.
    var createKeyReq = new V0Dtos.CreateInstallerKeyRequestDto(
      tenantResult.TenantId,
      sa.Id,
      CreatorKind.ServerServiceAccount,
      InstallerKeyType.Persistent,
      FriendlyName: "E2E Test Key");

    var keyResponse = await saClient.PostAsJsonAsync(
      HttpConstants.V0.InstallerKeysEndpoint,
      createKeyReq,
      TestContext.Current.CancellationToken);

    Assert.True(keyResponse.IsSuccessStatusCode,
      $"Create installer key failed: {keyResponse.StatusCode}");

    var keyResult = await keyResponse.Content
      .ReadFromJsonAsync<V0Dtos.CreateInstallerKeyResponseDto>(TestContext.Current.CancellationToken);

    Assert.NotNull(keyResult);
    Assert.NotEqual(Guid.Empty, keyResult.Id);

    // Step 4: The agent sends this request during installation, using the supplied installer key.
    var deviceDto = CreateDeviceUpdateDto(tenantResult.TenantId);
    var deviceId = deviceDto.Id;

    var createDeviceReq = new InternalDtos.CreateDeviceRequestDto(
      deviceDto,
      keyResult.Id,
      keyResult.KeySecret);

    using var anonClient = testServer.TestServer.CreateClient();

    var deviceResponse = await anonClient.PostAsJsonAsync(
      HttpConstants.Agent.DevicesEndpoint,
      createDeviceReq,
      TestContext.Current.CancellationToken);

    Assert.True(deviceResponse.IsSuccessStatusCode,
      $"Create device failed: {deviceResponse.StatusCode}");

    // Step 5: Get active desktop sessions.
    await BringDeviceOnline(testServer, deviceId);

    var sessionsResponse = await saClient.GetAsync(
      $"{HttpConstants.V0.DevicesEndpoint}/{deviceId}/desktop-sessions",
      TestContext.Current.CancellationToken);

    Assert.True(sessionsResponse.IsSuccessStatusCode,
      $"Get desktop sessions failed: {sessionsResponse.StatusCode}");

    var sessions = await sessionsResponse.Content
      .ReadFromJsonAsync<V0Dtos.DesktopSessionResponseDto[]>(TestContext.Current.CancellationToken);

    Assert.NotNull(sessions);
    Assert.Equal(2, sessions.Length);

    AssertConsoleSession(sessions!);
    AssertRdpSession(sessions!);

    // Step 6: Service account creates a logon token for an external user (dynamically creates transient external user).
    var createLogonTokenDto = new V0Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: deviceId,
      TenantId: tenantResult.TenantId,
      UserCorrelationId: "e2e-integration-service",
      ExpirationMinutes: 15);

    var tokenResponse = await saClient.PostAsJsonAsync(
      $"{HttpConstants.V0.LogonTokensEndpoint}/external",
      createLogonTokenDto,
      TestContext.Current.CancellationToken);

    Assert.True(tokenResponse.IsSuccessStatusCode,
      $"Create logon token failed: {tokenResponse.StatusCode}");

    var tokenResult = await tokenResponse.Content
      .ReadFromJsonAsync<V0Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);

    Assert.NotNull(tokenResult);
    Assert.NotNull(tokenResult.Token);

    // Step 7: Authenticate to /device-access/ using the logon token.
    using var deviceAccessClient = testServer.Factory.CreateClient(
      new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
      
    var deviceAccessResponse = await deviceAccessClient.GetAsync(
      $"/device-access?deviceId={deviceId}&logonToken={tokenResult.Token}",
      TestContext.Current.CancellationToken);

    Assert.True(
      deviceAccessResponse.IsSuccessStatusCode ||
      deviceAccessResponse.StatusCode == HttpStatusCode.Redirect ||
      deviceAccessResponse.StatusCode == HttpStatusCode.Found,
      $"Device access failed: {deviceAccessResponse.StatusCode}");

    // Step 8: Authenticated session can retrieve device info via the Internal namespace.
    var deviceInfoResponse = await deviceAccessClient.GetAsync(
      $"{HttpConstants.Internal.DevicesEndpoint}/{deviceId}",
      TestContext.Current.CancellationToken);

    Assert.True(deviceInfoResponse.IsSuccessStatusCode,
      $"Get device info failed: {deviceInfoResponse.StatusCode}");
    var deviceInfo = await deviceInfoResponse.Content
      .ReadFromJsonAsync<InternalDtos.DeviceResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(deviceInfo);
    Assert.Equal(deviceId, deviceInfo.Id);
    Assert.Equal("E2E Test Device", deviceInfo.Name);
  }

  private static void AssertConsoleSession(V0Dtos.DesktopSessionResponseDto[] sessions)
  {
    var consoleSession = sessions.First(x => x.Type == DesktopSessionType.Console);
    Assert.Equal("Console", consoleSession.Name);
    Assert.Equal("Default", consoleSession.DesktopName);
    Assert.Equal(1, consoleSession.SystemSessionId);
    Assert.Equal(1234, consoleSession.ProcessId);
    Assert.Equal("ConsoleUser", consoleSession.Username);
    Assert.True(consoleSession.AreRemoteControlPermissionsGranted);
  }

  private static void AssertRdpSession(V0Dtos.DesktopSessionResponseDto[] sessions)
  {
    var rdpSession = sessions.First(x => x.Type == DesktopSessionType.Rdp);
    Assert.Equal("RDP-Tcp#0", rdpSession.Name);
    Assert.Equal("Default", rdpSession.DesktopName);
    Assert.Equal(2, rdpSession.SystemSessionId);
    Assert.Equal(5678, rdpSession.ProcessId);
    Assert.Equal("RdpUser", rdpSession.Username);
    Assert.True(rdpSession.AreRemoteControlPermissionsGranted);
  }

  private static async Task BringDeviceOnline(TestWebServer testServer, Guid deviceId)
  {
    using var scope = testServer.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var device = await db.Devices.FirstAsync(x => x.Id == deviceId, TestContext.Current.CancellationToken);
    device.IsOnline = true;
    device.ConnectionId = "test-agent-connection-id";
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static DesktopSession[] CreateFakeDesktopSessions()
  {
    return
    [
      new DesktopSession
      {
        AreRemoteControlPermissionsGranted = true,
        DesktopName = "Default",
        Name = "Console",
        ProcessId = 1234,
        SystemSessionId = 1,
        Type = DesktopSessionType.Console,
        Username = "ConsoleUser"
      },
      new DesktopSession
      {
        AreRemoteControlPermissionsGranted = true,
        DesktopName = "Default",
        Name = "RDP-Tcp#0",
        ProcessId = 5678,
        SystemSessionId = 2,
        Type = DesktopSessionType.Rdp,
        Username = "RdpUser"
      }
    ];
  }

  private static Mock<IHubContext<AgentHub, IAgentHubClient>> CreateMockAgentHubContext(
    DesktopSession[] fakeDesktopSessions)
  {
    var mockAgentClient = new Mock<IAgentHubClient>();
    mockAgentClient
      .Setup(x => x.GetActiveDesktopSessions())
      .ReturnsAsync(fakeDesktopSessions);

    var mockHubClients = new Mock<IHubClients<IAgentHubClient>>();
    mockHubClients
      .Setup(x => x.Client(It.IsAny<string>()))
      .Returns(mockAgentClient.Object);

    var mockAgentHubContext = new Mock<IHubContext<AgentHub, IAgentHubClient>>();
    mockAgentHubContext
      .Setup(x => x.Clients)
      .Returns(mockHubClients.Object);

    return mockAgentHubContext;
  }

  private DeviceUpdateRequestDto CreateDeviceUpdateDto(Guid tenantId)
  {
    var deviceId = Guid.NewGuid();
    var deviceDto = new DeviceUpdateRequestDto(
      Id: deviceId,
      TenantId: tenantId,
      Name: "E2E Test Device",
      AgentVersion: "1.0.0",
      Is64Bit: true,
      OsArchitecture: Architecture.X64,
      OsDescription: "Windows 10",
      Platform: SystemPlatform.Windows,
      ProcessorCount: 4,
      CpuUtilization: 10,
      TotalMemory: 8192,
      TotalStorage: 256000,
      UsedMemory: 4096,
      UsedStorage: 128000,
      CurrentUsers: ["TestUser"],
      MacAddresses: ["00:11:22:33:44:55"],
      LocalIpV4: "10.0.0.1",
      LocalIpV6: "fe80::1",
      Drives: [new Drive { Name = "C:", TotalSize = 256000, FreeSpace = 128000 }]);

    return deviceDto;
  }
}
