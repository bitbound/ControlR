using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerAlerts;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PublicServerSettings;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerStats;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserServerSettings;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// The public, diagnostic, and client-capability surfaces that moved to V1. Each was excluded
/// from V1 on handler shape alone, which is not a reason. A pre-auth probe and a live-device
/// session are both valid V1 targets when their payloads are JSON.
/// </summary>
public class ServerDiagnosticsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetFileUploadMaxSize_WhenAnonymous_ReturnsUnauthorized()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.UserServerSettingsEndpoint}/file-upload-max-size",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetFileUploadMaxSize_WithUserPrincipal_ReturnsConfiguredLimit()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, _, _) = await scope.CreateControllerWithTestData<UserServerSettingsController>();

    var result = controller.GetFileUploadMaxSize(
      scope.ServiceProvider.GetRequiredService<IOptionsMonitor<AppOptions>>());

    var response = Assert.IsType<FileUploadMaxSizeResponseDto>(
      Assert.IsType<OkObjectResult>(result.Result).Value);
    Assert.Equal(
      scope.ServiceProvider.GetRequiredService<IOptionsMonitor<AppOptions>>().CurrentValue.MaxFileTransferSize,
      response.MaxFileSize);
  }

  [Fact]
  public async Task GetPublicServerSettings_WhenAnonymous_ReturnsOk()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var response = await httpClient.GetAsync(
      HttpConstants.V1.PublicServerSettingsEndpoint,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var settings = await response.Content.ReadFromJsonAsync<PublicServerSettingsDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(settings);
  }

  [Fact]
  public async Task GetServerAlert_WhenAnonymous_ReturnsUnauthorized()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var response = await httpClient.GetAsync(
      HttpConstants.V1.ServerAlertEndpoint,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetServerStats_WhenAnonymous_ReturnsUnauthorized()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var response = await httpClient.GetAsync(
      HttpConstants.V1.ServerStatsEndpoint,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetServerStats_WithServerPrincipal_ReturnsStats()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    await scope.ServiceProvider.CreateTestTenant("Stats Tenant");
    var controller = await scope.CreateControllerWithServerPrincipal<ServerStatsController>();

    var result = await controller.GetServerStats();

    var stats = Assert.IsType<ServerStatsDto>(result.Value);
    Assert.True(stats.TotalTenants >= 1);
  }

  [Fact]
  public async Task GetServerVersion_WhenAnonymous_ReturnsOk()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.VersionEndpoint}/server",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.False(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
  }

  [Fact]
  public async Task SendTestEmail_WhenAnonymous_ReturnsUnauthorized()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var response = await httpClient.PostAsync(
      HttpConstants.V1.TestEmailEndpoint,
      content: null,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task UpdateServerAlert_WithServerPrincipal_PersistsAlert()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var controller = await scope.CreateControllerWithServerPrincipal<ServerAlertController>();

    var request = new ServerAlertRequestDto(
      "Scheduled maintenance",
      MessageSeverity.Warning,
      IsDismissable: true,
      IsSticky: false,
      IsEnabled: true);

    var result = await controller.UpdateAlert(request);

    var response = Assert.IsType<ServerAlertResponseDto>(result.Value);
    Assert.Equal("Scheduled maintenance", response.Message);
    Assert.True(response.HasAlertSet);
  }
}
