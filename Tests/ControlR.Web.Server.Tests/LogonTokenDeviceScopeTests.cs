using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class LogonTokenDeviceScopeTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task LogonTokenSession_ShouldBeRestrictedToSingleDevice()
  {
    using var testApp = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testApp.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var primaryDeviceId = Guid.NewGuid();
    var otherDeviceId = Guid.NewGuid();

    // Setup tenant + devices + user
    var tenant = await testApp.TestServer.Services.CreateTestTenant();
    await testApp.TestServer.Services.CreateTestDevice(tenant.Id, primaryDeviceId);
    await testApp.TestServer.Services.CreateTestDevice(tenant.Id, otherDeviceId);
    var user = await testApp.TestServer.Services.CreateTestUser(tenant.Id);

    // Create PAT
    var patManager = testApp.TestServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patCreate = await patManager.CreateToken(new CreatePersonalAccessTokenRequestDto("ScopeTest PAT"), tenant.Id, user.Id);
    Assert.True(patCreate.IsSuccess, patCreate.Reason);
    var pat = patCreate.Value.PlainTextToken;

    // Request logon token for primary device
    httpClient.DefaultRequestHeaders.Add(PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName, pat);
    var logonTokenRequest = new LogonTokenRequestDto { DeviceId = primaryDeviceId, ExpirationMinutes = 5 };
    var logonTokenResponse = await httpClient.PostAsJsonAsync("/api/logon-tokens", logonTokenRequest);
    logonTokenResponse.EnsureSuccessStatusCode();
    var logonTokenResult = await logonTokenResponse.Content.ReadFromJsonAsync<LogonTokenResponseDto>();
    Assert.NotNull(logonTokenResult);

    // Consume logon token (first access) to establish cookie session
    var firstAccess = await httpClient.GetAsync(logonTokenResult!.DeviceAccessUrl);
    Assert.True(firstAccess.IsSuccessStatusCode || firstAccess.StatusCode == HttpStatusCode.Redirect || firstAccess.StatusCode == HttpStatusCode.Found);

    // Remove PAT header so that subsequent API requests use the established cookie session
    httpClient.DefaultRequestHeaders.Remove(PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName);

    // Attempt to access primary device API (should succeed)
    var primaryDeviceApi = await httpClient.GetAsync($"/api/devices/{primaryDeviceId}");
    Assert.True(primaryDeviceApi.IsSuccessStatusCode, $"Expected success for primary device, got {primaryDeviceApi.StatusCode}");

    // Attempt to access other device API (should be forbidden due to DeviceSessionScope restriction)
    var otherDeviceApi = await httpClient.GetAsync($"/api/devices/{otherDeviceId}");
    Assert.True(
      otherDeviceApi.StatusCode == HttpStatusCode.Forbidden || otherDeviceApi.StatusCode == HttpStatusCode.Unauthorized,
      $"Expected Forbidden/Unauthorized for other device, got {otherDeviceApi.StatusCode}");
  }
}
