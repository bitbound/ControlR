using System.Net;
using ControlR.Web.Client;
using ControlR.Web.Server.Tests.Helpers;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins the behavior of the <c>AppOptions:DisableMainUi</c> option: only device access pages stay
/// visible, every other UI route (including the Identity account pages) renders the not-found view,
/// and the API surface is untouched.
/// </summary>
public class DisableMainUiTests(ITestOutputHelper testOutput)
{
  private const string ForgotPasswordMarker = "Forgot your password?";
  private const string LoginPageMarker = "Use a local account to log in.";
  private const string NavChromeMarker = "mud-navmenu";
  private const string NotFoundActionsMarker = "Go Home";
  private const string NotFoundMarker = "Page Not Found";
  private const string UnauthorizedMarker = "You are not signed in, or your access has expired.";

  [Fact]
  public async Task AccountForgotPassword_RendersForm_WhenMainUiIsEnabledByDefault()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);

    var page = await (await testServer.GetHttpClient())
      .GetStringAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);

    Assert.Contains(ForgotPasswordMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NotFoundMarker, page, StringComparison.Ordinal);
  }

  [Fact]
  public async Task AccountForgotPassword_RendersNotFound_WhenMainUiDisabled()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput, settings: DisabledSettings());

    var page = await (await testServer.GetHttpClient())
      .GetStringAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);

    Assert.Contains(NotFoundMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(ForgotPasswordMarker, page, StringComparison.Ordinal);
  }

  [Fact]
  public async Task AccountLogin_RendersLoginForm_WhenMainUiIsEnabledByDefault()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);

    var page = await (await testServer.GetHttpClient())
      .GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);

    Assert.Contains(LoginPageMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NotFoundMarker, page, StringComparison.Ordinal);
  }

  [Fact]
  public async Task AccountLogin_RendersNotFound_WhenMainUiDisabled()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput, settings: DisabledSettings());

    var page = await (await testServer.GetHttpClient())
      .GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);

    Assert.Contains(NotFoundMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(LoginPageMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NotFoundActionsMarker, page, StringComparison.Ordinal);
  }

  [Fact]
  public async Task DeviceAccess_RedirectsToLogin_WhenMainUiIsEnabledByDefault()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var client = testServer.Factory.CreateClient(
      new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.GetAsync("/device-access", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
  }

  [Fact]
  public async Task DeviceAccess_RetainsItsChallengeRedirect_WhenMainUiDisabled()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput, settings: DisabledSettings());
    using var client = testServer.Factory.CreateClient(
      new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.GetAsync("/device-access", TestContext.Current.CancellationToken);

    // Identity pages render as not-found when the UI is gated, so the challenge must not point at them.
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal(ClientRoutes.Unauthorized, response.Headers.Location?.OriginalString);
  }

  [Fact]
  public async Task HomePage_RendersNotFound_WhenMainUiDisabled()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput, settings: DisabledSettings());

    var page = await (await testServer.GetHttpClient())
      .GetStringAsync("/", TestContext.Current.CancellationToken);

    Assert.Contains(NotFoundMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NavChromeMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NotFoundActionsMarker, page, StringComparison.Ordinal);
  }

  [Fact]
  public async Task HomePage_RendersUiChrome_WhenMainUiIsEnabledByDefault()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);

    var page = await (await testServer.GetHttpClient())
      .GetStringAsync("/", TestContext.Current.CancellationToken);

    Assert.Contains(NavChromeMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NotFoundMarker, page, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnauthorizedPage_RendersWithoutChrome_RegardlessOfMainUiGate(bool disableMainUi)
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      testOutput,
      settings: disableMainUi ? DisabledSettings() : null);

    var page = await (await testServer.GetHttpClient())
      .GetStringAsync(ClientRoutes.Unauthorized, TestContext.Current.CancellationToken);

    Assert.Contains(UnauthorizedMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NotFoundMarker, page, StringComparison.Ordinal);
    Assert.DoesNotContain(NavChromeMarker, page, StringComparison.Ordinal);
  }

  [Fact]
  public async Task V1Api_RespondsIdentically_WhenMainUiDisabled()
  {
    using var disabledServer = await TestWebServerBuilder.CreateTestServer(testOutput, settings: DisabledSettings());
    using var defaultServer = await TestWebServerBuilder.CreateTestServer(testOutput);

    using var disabledClient = await disabledServer.GetHttpClient();
    using var defaultClient = await defaultServer.GetHttpClient();

    var disabledResponse = await disabledClient.GetAsync("/api/v1/devices", TestContext.Current.CancellationToken);
    var defaultResponse = await defaultClient.GetAsync("/api/v1/devices", TestContext.Current.CancellationToken);

    // The gate is a UI-layer concern only. An unauthenticated V1 call must not turn into a
    // not-found page or a login redirect because of it.
    Assert.Equal(defaultResponse.StatusCode, disabledResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, disabledResponse.StatusCode);
  }

  private static Dictionary<string, string?> DisabledSettings() => new()
  {
    ["AppOptions:DisableMainUi"] = "true"
  };
}
