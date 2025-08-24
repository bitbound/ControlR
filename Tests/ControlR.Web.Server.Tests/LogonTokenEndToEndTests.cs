using System.Net.Http.Json;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
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
    var httpClient = testApp.HttpClient;
    var deviceId = Guid.NewGuid();

    // Phase 1: Create an API key using the service directly (for simplicity)
    var tenant = await testApp.CreateTestTenant();
    var apiKeyManager = testApp.App.Services.GetRequiredService<IApiKeyManager>();
    var createApiKeyRequest = new CreateApiKeyRequestDto("Test API Key for Logon Token");
    var createResult = await apiKeyManager.CreateKey(createApiKeyRequest, tenant.Id);
    
    Assert.True(createResult.IsSuccess, $"API key creation failed: {createResult.Reason}");
    var apiKey = createResult.Value!.PlainTextKey;

    // Phase 2: Use the API key to request a logon token via HTTP
    var logonTokenRequest = new LogonTokenRequestDto
    {
      DeviceId = deviceId,
      ExpirationMinutes = 15,
      DisplayName = "Test User"
    };

    // Add API key to request headers
    httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

    var content = await httpClient.GetStringAsync("/");
    
    var logonTokenResponse = await httpClient.PostAsJsonAsync("/api/logon-tokens", logonTokenRequest);
    
    // Assert logon token creation succeeded
    logonTokenResponse.EnsureSuccessStatusCode();
    var logonTokenResult = await logonTokenResponse.Content.ReadFromJsonAsync<LogonTokenResponseDto>();
    
    Assert.NotNull(logonTokenResult);
    Assert.NotNull(logonTokenResult.Token);
    Assert.NotNull(logonTokenResult.DeviceAccessUrl);

    // Phase 3: Use the logon token to access the device-access page (first time - should succeed)
    var deviceAccessUri = logonTokenResult.DeviceAccessUrl;
    
    // Clear API key header for device access (uses different auth)
    httpClient.DefaultRequestHeaders.Remove("x-api-key");
    
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
      secondAccessResponse.StatusCode == System.Net.HttpStatusCode.Redirect ||
      secondAccessResponse.StatusCode == System.Net.HttpStatusCode.Found,
      $"Expected unauthorized, forbidden, or redirect on second use, but got {secondAccessResponse.StatusCode}");

    // Additional verification: Extract token and device ID from URL to verify they match
    var query = System.Web.HttpUtility.ParseQueryString(deviceAccessUri.Query);
    var tokenParam = query.Get("logonToken");
    var deviceIdParam = query.Get("deviceId");
    
    Assert.Equal(logonTokenResult.Token, tokenParam);
    Assert.Equal(deviceId.ToString(), deviceIdParam);
  }
}
