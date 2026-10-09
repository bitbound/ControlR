using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class AgentDeploymentOptionsEndpointTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Get_WhenTheInstanceIdFeatureIsDisabled_ReportsNoInstanceId()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (client, tenantId, deviceId, keyProvider, keyPair) = await SetupTenantAndDevice(testServer);

    await SetInstanceIdSettings(testServer, tenantId, appendInstanceId: false, instanceId: "ce");

    using var request = CreateSignedRequest(keyProvider, keyPair, deviceId, "disabled");
    var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

    response.EnsureSuccessStatusCode();
    var options = await response.Content.ReadFromJsonAsync<AgentDeploymentOptionsDto>(TestContext.Current.CancellationToken);

    // A tenant that has not switched the feature on has no opinion, which is not a request for an
    // empty instance id.
    Assert.NotNull(options);
    Assert.Null(options.InstanceId);
  }

  [Fact]
  public async Task Get_WhenTheInstanceIdIsEmpty_ReportsNoInstanceId()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (client, tenantId, deviceId, keyProvider, keyPair) = await SetupTenantAndDevice(testServer);

    await SetInstanceIdSettings(testServer, tenantId, appendInstanceId: true, instanceId: null);

    using var request = CreateSignedRequest(keyProvider, keyPair, deviceId, "empty");
    var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

    response.EnsureSuccessStatusCode();
    var options = await response.Content.ReadFromJsonAsync<AgentDeploymentOptionsDto>(TestContext.Current.CancellationToken);

    Assert.NotNull(options);
    Assert.Null(options.InstanceId);
  }

  [Fact]
  public async Task Get_WhenTheSignatureNamesAnotherTenant_ReportsThatTenantsInstanceId()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);

    // Two tenants with different instance ids. The signature is what says which device, and therefore
    // which tenant, is asking, so the answer has to follow the signer rather than the deployment.
    var (clientA, tenantIdA, deviceIdA, keyProviderA, keyPairA) = await SetupTenantAndDevice(testServer);
    await SetInstanceIdSettings(testServer, tenantIdA, appendInstanceId: true, instanceId: "tenant-a");

    using (var requestA = CreateSignedRequest(keyProviderA, keyPairA, deviceIdA, "tenant-a"))
    {
      var responseA = await clientA.SendAsync(requestA, TestContext.Current.CancellationToken);
      responseA.EnsureSuccessStatusCode();
      var optionsA = await responseA.Content.ReadFromJsonAsync<AgentDeploymentOptionsDto>(TestContext.Current.CancellationToken);
      Assert.Equal("tenant-a", optionsA!.InstanceId);
    }

    var (clientB, tenantIdB, deviceIdB, keyProviderB, keyPairB) = await SetupTenantAndDevice(testServer);
    await SetInstanceIdSettings(testServer, tenantIdB, appendInstanceId: true, instanceId: "tenant-b");

    using var requestB = CreateSignedRequest(keyProviderB, keyPairB, deviceIdB, "tenant-b");
    var responseB = await clientB.SendAsync(requestB, TestContext.Current.CancellationToken);
    responseB.EnsureSuccessStatusCode();
    var optionsB = await responseB.Content.ReadFromJsonAsync<AgentDeploymentOptionsDto>(TestContext.Current.CancellationToken);

    Assert.Equal("tenant-b", optionsB!.InstanceId);
  }

  [Fact]
  public async Task Get_WhenTheSignatureWasMadeForAnotherRequest_IsRejected()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (client, tenantId, deviceId, keyProvider, keyPair) = await SetupTenantAndDevice(testServer);

    await SetInstanceIdSettings(testServer, tenantId, appendInstanceId: true, instanceId: "ce");

    // The verb and path are inside the signature, so a header captured from one call cannot be
    // replayed against a different one.
    var attestation = new AgentRequestAttestationDto(deviceId, "GET", "/api/agent/updates/get-bundle-metadata/WinX64");
    var signedDto = keyProvider.Sign(attestation, keyPair.PrivateKey, Convert.ToBase64String(keyPair.PublicKey));

    using var request = new HttpRequestMessage(HttpMethod.Get, HttpConstants.Agent.DeploymentOptionsEndpoint);
    request.Headers.TryAddWithoutValidation(AgentSignatureHeader.Name, AgentSignatureHeader.Encode(signedDto));

    var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Get_WithASignedRequest_ReportsTheTenantsInstanceId()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (client, tenantId, deviceId, keyProvider, keyPair) = await SetupTenantAndDevice(testServer);

    await SetInstanceIdSettings(testServer, tenantId, appendInstanceId: true, instanceId: "ce");

    using var request = CreateSignedRequest(keyProvider, keyPair, deviceId, "signed");
    var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

    response.EnsureSuccessStatusCode();
    var options = await response.Content.ReadFromJsonAsync<AgentDeploymentOptionsDto>(TestContext.Current.CancellationToken);

    Assert.NotNull(options);
    Assert.Equal("ce", options.InstanceId);
  }

  [Fact]
  public async Task Get_WithNoSignature_IsRejected()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (client, _, _, _, _) = await SetupTenantAndDevice(testServer);

    var response = await client.GetAsync(
      HttpConstants.Agent.DeploymentOptionsEndpoint,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  private static HttpRequestMessage CreateSignedRequest(
    IEd25519KeyProvider keyProvider,
    Ed25519KeyPair keyPair,
    Guid deviceId,
    string marker)
  {
    // The marker keeps each signed payload distinct, so no two cases share a signature.
    var pathAndQuery = $"{HttpConstants.Agent.DeploymentOptionsEndpoint}?case={marker}";
    var attestation = new AgentRequestAttestationDto(deviceId, HttpMethod.Get.Method, pathAndQuery);
    var signedDto = keyProvider.Sign(attestation, keyPair.PrivateKey, Convert.ToBase64String(keyPair.PublicKey));

    var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
    request.Headers.TryAddWithoutValidation(AgentSignatureHeader.Name, AgentSignatureHeader.Encode(signedDto));
    return request;
  }

  private static async Task SetInstanceIdSettings(
    TestWebServer testServer,
    Guid tenantId,
    bool appendInstanceId,
    string? instanceId)
  {
    var manager = testServer.TestServer.Services.GetRequiredService<ITenantSettingsManager>();

    await manager.SetSettings(
      tenantId,
      new InternalDtos.TenantSettingsDto(appendInstanceId, instanceId, null),
      TestContext.Current.CancellationToken);
  }

  private async Task<(HttpClient Client, Guid TenantId, Guid DeviceId, IEd25519KeyProvider KeyProvider, Ed25519KeyPair KeyPair)> SetupTenantAndDevice(
    TestWebServer testServer)
  {
    var tenant = await testServer.TestServer.Services.CreateTestTenant();
    var keyProvider = testServer.TestServer.Services.GetRequiredService<IEd25519KeyProvider>();
    var keyPair = keyProvider.GenerateKeyPair();
    var device = await testServer.TestServer.Services.CreateTestDevice(
      tenant.Id,
      publicKeyBase64: Convert.ToBase64String(keyPair.PublicKey));

    return (testServer.TestServer.CreateClient(), tenant.Id, device.Id, keyProvider, keyPair);
  }
}
