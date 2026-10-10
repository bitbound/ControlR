using ControlR.Web.Client.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins that the public base URL surfaced in <c>PublicServerSettings</c> comes from the configured
/// <c>AppOptions:PublicBaseUrl</c> through <c>IPublicUrlProvider</c>, and stays null when unset.
/// </summary>
public class PublicServerSettingsProviderServerTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task GetPublicServerSettings_WhenPublicBaseUrlConfigured_PopulatesPublicBaseUrl()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      extraConfiguration: new Dictionary<string, string?> { ["AppOptions:PublicBaseUrl"] = "https://controlr.test" });

    using var scope = testApp.CreateScope();
    var provider = scope.ServiceProvider.GetRequiredService<IPublicServerSettingsProvider>();

    var settings = await provider.GetPublicServerSettings();

    Assert.Equal("https://controlr.test", settings.PublicBaseUrl);
  }

  [Fact]
  public async Task GetPublicServerSettings_WhenPublicBaseUrlNotConfigured_LeavesPublicBaseUrlNull()
  {
    // appsettings.Testing.json, which the harness loads, sets a usable PublicBaseUrl. Simulating an
    // install that never configured one requires overriding it with the empty string.
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      extraConfiguration: new Dictionary<string, string?> { ["AppOptions:PublicBaseUrl"] = "" });

    using var scope = testApp.CreateScope();
    var provider = scope.ServiceProvider.GetRequiredService<IPublicServerSettingsProvider>();

    var settings = await provider.GetPublicServerSettings();

    Assert.Null(settings.PublicBaseUrl);
  }
}
