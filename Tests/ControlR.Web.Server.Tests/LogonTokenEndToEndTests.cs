using System.Net.Http.Json;
using ControlR.Libraries.Shared.Dtos.ServerApi;
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
  public async Task LogonTokenFlow_EndToEnd_ShouldWorkOnceAndFailOnSecondUse()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var options = new WebApplicationFactoryClientOptions() { AllowAutoRedirect = false };
    using var httpClient = testApp.Factory.CreateClient();

    var deviceId = Guid.NewGuid();

  // Phase 1: Create a tenant, device and a personal access token for a test user
  var tenant = await testApp.TestServer.Services.CreateTestTenant();

  // Create a device for the tenant
  await testApp.TestServer.Services.CreateTestDevice(tenant.Id, deviceId);

  // Create a test user and issue a personal access token for that user
  var user = await testApp.TestServer.Services.CreateTestUser(tenant.Id);
  var patManager = testApp.TestServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
  var createPatRequest = new ControlR.Libraries.Shared.Dtos.ServerApi.CreatePersonalAccessTokenRequestDto("Test Key for Logon Token");
  var createResult = await patManager.CreateToken(createPatRequest, tenant.Id, user.Id);

  Assert.True(createResult.IsSuccess, $"PAT creation failed: {createResult.Reason}");
  var apiKey = createResult.Value!.PlainTextToken;

    // Phase 2: Use the API key to request a logon token via HTTP
    var logonTokenRequest = new LogonTokenRequestDto
    {
      DeviceId = deviceId,
      ExpirationMinutes = 15,
      DisplayName = "Test User"
    };

  // Add personal access token to request headers as a Bearer token
  httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    
    var logonTokenResponse = await httpClient.PostAsJsonAsync("/api/logon-tokens", logonTokenRequest);
    
    // Assert logon token creation succeeded
    logonTokenResponse.EnsureSuccessStatusCode();
    var logonTokenResult = await logonTokenResponse.Content.ReadFromJsonAsync<LogonTokenResponseDto>();
    
    Assert.NotNull(logonTokenResult);
    Assert.NotNull(logonTokenResult.Token);
    Assert.NotNull(logonTokenResult.DeviceAccessUrl);

    // Phase 3: Use the logon token to access the device-access page (first time - should succeed)
    var deviceAccessUri = logonTokenResult.DeviceAccessUrl;
    
  // Clear Authorization header for device access (uses different auth)
  httpClient.DefaultRequestHeaders.Authorization = null;
    
    var firstAccessResponse = await httpClient.GetAsync(deviceAccessUri);
    
    // Assert first access succeeds (200 OK or redirect to authenticated page)
    Assert.True(
      firstAccessResponse.IsSuccessStatusCode || 
      firstAccessResponse.StatusCode == System.Net.HttpStatusCode.Redirect ||
      firstAccessResponse.StatusCode == System.Net.HttpStatusCode.Found,
      $"Expected success or redirect on first access, but got {firstAccessResponse.StatusCode}");

    // Phase 4: Try to use the same logon token URL again (should fail - token consumed)
    var secondAccessResponse = await httpClient.GetAsync(deviceAccessUri);
    
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
