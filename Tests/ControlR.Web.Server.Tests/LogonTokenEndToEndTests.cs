using System.Net.Http.Json;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class LogonTokenEndToEndTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task LogonTokenFlow_EndToEnd_ShouldNotAllowAccessToAnotherDevice()
  {
    // Arrange
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var options = new WebApplicationFactoryClientOptions() { AllowAutoRedirect = false };
    using var httpClient = testServer.Factory.CreateClient();

    var deviceId1 = Guid.NewGuid();
    var deviceId2 = Guid.NewGuid();

    // Phase 1: Create a user, tenant, and two devices
    var user = await testServer.TestServer.Services.CreateTestUser();
    var tenant = user.Tenant!;

    // Create two devices for the tenant
    await testServer.TestServer.Services.CreateTestDevice(tenant.Id, deviceId1);
    await testServer.TestServer.Services.CreateTestDevice(tenant.Id, deviceId2);

    // Create a test user and issue a personal access token for that user
    var patManager = testServer.TestServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var createPatRequest = new CreatePersonalAccessTokenRequestDto("Test Key for Cross-Device Access");
    var createResult = await patManager.CreateToken(createPatRequest, tenant.Id, user.Id);

    Assert.True(createResult.IsSuccess, $"PAT creation failed: {createResult.Reason}");
    var pat = createResult.Value.PlainTextToken;

    // Phase 2: Create a logon token for device1 only
    var logonTokenRequest = new LogonTokenRequestDto
    {
      DeviceId = deviceId1,
      ExpirationMinutes = 15,
    };

    // Add personal access token to request headers
    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      pat);

    var logonTokenResponse = await httpClient.PostAsJsonAsync("/api/logon-tokens", logonTokenRequest);

    // Assert logon token creation succeeded
    logonTokenResponse.EnsureSuccessStatusCode();
    var logonTokenResult = await logonTokenResponse.Content.ReadFromJsonAsync<LogonTokenResponseDto>();

    Assert.NotNull(logonTokenResult);
    Assert.NotNull(logonTokenResult.Token);

    var logonToken = logonTokenResult.Token;

    using var newClient = testServer.TestServer.CreateClient();

    var device2DetailsResponse = await newClient.GetAsync(
      $"/device-access?deviceId={deviceId2}&logonToken={logonToken}");
  
    // This should fail - the logon token should only grant access to device1
    Assert.True(
      device2DetailsResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
      device2DetailsResponse.StatusCode == System.Net.HttpStatusCode.Forbidden ||
      device2DetailsResponse.StatusCode == System.Net.HttpStatusCode.NotFound,
      $"Expected unauthorized, forbidden, or not found when accessing device2 with device1 logon token, but got {device2DetailsResponse.StatusCode}");
  }

  [Fact]
  public async Task LogonTokenFlow_EndToEnd_ShouldWorkOnceAndFailOnSecondUse()
  {
    // Arrange
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var options = new WebApplicationFactoryClientOptions() { AllowAutoRedirect = false };
    using var httpClient = testServer.Factory.CreateClient();

    var deviceId = Guid.NewGuid();

    // Phase 1: Create a user, tenant, device and a personal access token for a test user
    var user = await testServer.TestServer.Services.CreateTestUser();
    var tenant = user.Tenant!;

    // Create a device for the tenant
    await testServer.TestServer.Services.CreateTestDevice(tenant.Id, deviceId);

    // Create a test user and issue a personal access token for that user
    var patManager = testServer.TestServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var createPatRequest = new CreatePersonalAccessTokenRequestDto("Test Key for Logon Token");
    var createResult = await patManager.CreateToken(createPatRequest, tenant.Id, user.Id);

    Assert.True(createResult.IsSuccess, $"PAT creation failed: {createResult.Reason}");
    var pat = createResult.Value.PlainTextToken;

    // Phase 2: Use the API key to request a logon token via HTTP
    var logonTokenRequest = new LogonTokenRequestDto
    {
      DeviceId = deviceId,
      ExpirationMinutes = 15,
    };

    // Add personal access token to request headers
    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      pat);

    var logonTokenResponse = await httpClient.PostAsJsonAsync("/api/logon-tokens", logonTokenRequest);

    // Assert logon token creation succeeded
    logonTokenResponse.EnsureSuccessStatusCode();
    var logonTokenResult = await logonTokenResponse.Content.ReadFromJsonAsync<LogonTokenResponseDto>();

    Assert.NotNull(logonTokenResult);
    Assert.NotNull(logonTokenResult.Token);
    Assert.NotNull(logonTokenResult.DeviceAccessUrl);

    // Phase 3: Use the logon token to access the device-access page (first time - should succeed)
    var deviceAccessUri = logonTokenResult.DeviceAccessUrl;

    using var newClient = testServer.TestServer.CreateClient();

    var firstAccessResponse = await newClient.GetAsync(deviceAccessUri);

    // Assert first access succeeds (200 OK or redirect to authenticated page)
    Assert.True(
      firstAccessResponse.IsSuccessStatusCode ||
      firstAccessResponse.StatusCode == System.Net.HttpStatusCode.Redirect ||
      firstAccessResponse.StatusCode == System.Net.HttpStatusCode.Found,
      $"Expected success or redirect on first access, but got {firstAccessResponse.StatusCode}");

    // Phase 4: Try to use the same logon token URL again (should fail - token consumed)
    var secondAccessResponse = await newClient.GetAsync(deviceAccessUri);

    // The token should be consumed, so this should either:
    // - Return Unauthorized (401)
    // - Return Forbidden (403) 
    // - Redirect to login
    Assert.True(
      secondAccessResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
      secondAccessResponse.StatusCode == System.Net.HttpStatusCode.Forbidden ||
      secondAccessResponse.StatusCode == System.Net.HttpStatusCode.Redirect,
      $"Expected unauthorized, forbidden, or redirect on second use, but got {secondAccessResponse.StatusCode}");

    // Additional verification: Extract token and device ID from URL to verify they match
    var query = System.Web.HttpUtility.ParseQueryString(deviceAccessUri.Query);
    var tokenParam = query.Get("logonToken");
    var deviceIdParam = query.Get("deviceId");

    Assert.Equal(logonTokenResult.Token, tokenParam);
    Assert.Equal(deviceId.ToString(), deviceIdParam);
  }
}
