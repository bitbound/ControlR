using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountEndToEndTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task ServiceAccount_EndToEnd_CompletesFullProvisioningFlow()
  {
    // Step 1: Turn self-registration off and bootstrap a service account on startup.
    var bootstrapTokenId = Guid.NewGuid();
    const string bootstrapSecret = "a-very-strong-bootstrap-secret-key-32-long";
    const string saName = "bootstrap-sa";

    var config = new Dictionary<string, string?>
    {
      ["AppOptions:DisableEmailSending"] = "true",
      ["AppOptions:EnableFirstUserSelfRegistration"] = "false",
      ["Bootstrap:ServerServiceAccountName"] = saName,
      ["Bootstrap:ServerServiceAccountTokenId"] = $"{bootstrapTokenId}",
      ["Bootstrap:ServerServiceAccountTokenSecret"] = bootstrapSecret,
    };

    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput, settings: config);

    var services = testServer.Services;
    var saManager = services.GetRequiredService<IServiceAccountManager>();
    var bootstrapOptions = services.GetRequiredService<IOptions<BootstrapOptions>>();
    var appLifetime = services.GetRequiredService<IHostApplicationLifetime>();
    var bootstrapResult = await saManager.BootstrapServerServiceAccount(
      bootstrapOptions.Value,
      appLifetime.ApplicationStopping);

    Assert.True(bootstrapResult.IsSuccess, $"Bootstrap failed: {bootstrapResult.Reason}");

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
    var saAccounts = await saManager.GetAllServer(TestContext.Current.CancellationToken);
    var sa = saAccounts.First(s => s.Name == saName);

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

    // Step 4: Anonymous request creates a device using the installer key.
    var deviceId = Guid.NewGuid();
    var deviceDto = new DeviceUpdateRequestDto(
      Id: deviceId,
      TenantId: tenantResult.TenantId,
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

    // Step 5: Service account creates a logon token (dynamically creates transient external user).
    var createLogonTokenReq = new V0Dtos.CreateLogonTokenRequestDto(
      DeviceId: deviceId,
      TenantId: tenantResult.TenantId,
      UserId: null,
      UserCorrelationId: "e2e-integration-service",
      ExpirationMinutes: 15);

    var tokenResponse = await saClient.PostAsJsonAsync(
      HttpConstants.V0.LogonTokensEndpoint,
      createLogonTokenReq,
      TestContext.Current.CancellationToken);

    Assert.True(tokenResponse.IsSuccessStatusCode,
      $"Create logon token failed: {tokenResponse.StatusCode}");
    var tokenResult = await tokenResponse.Content
      .ReadFromJsonAsync<V0Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(tokenResult);
    Assert.NotNull(tokenResult.Token);

    // Step 6: Authenticate to /device-access/ using the logon token.
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

    // Step 7: Authenticated session can retrieve device info via the Internal namespace.
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
}
