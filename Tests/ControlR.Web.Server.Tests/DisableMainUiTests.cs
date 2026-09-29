using System.Net;
using System.Text;
using System.Text.Json;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins the behavior of the <c>AppOptions:DisableMainUi</c> option: only device access pages stay
/// visible, every other UI route (including the Identity account pages) renders the not-found view,
/// and the API surface is untouched.
/// </summary>
/// <remarks>
/// Ref: https://github.com/bitbound/ControlR-dev/issues/195
/// </remarks>
public class DisableMainUiTests(ITestOutputHelper testOutput)
{
  private const string ForgotPasswordMarker = "Forgot your password?";
  private const string LoginPageMarker = "Use a local account to log in.";
  private const string NavChromeMarker = "mud-navmenu";
  private const string NotFoundActionsMarker = "Go Home";
  private const string NotFoundMarker = "Page Not Found";

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
  public async Task DeviceAccess_RetainsItsChallengeRedirect_WhenMainUiDisabled()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput, settings: DisabledSettings());
    using var client = testServer.Factory.CreateClient(
      new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.GetAsync("/device-access", TestContext.Current.CancellationToken);

    // Device access keeps its normal unauthenticated behavior instead of being replaced by the
    // not-found view. Token-bearing callers continue to land on the device access pages.
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    Assert.Contains("device-access", response.Headers.Location?.Query ?? string.Empty, StringComparison.Ordinal);
    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    Assert.DoesNotContain(NotFoundMarker, body, StringComparison.Ordinal);
  }

  [Fact]
  public async Task HomePage_PersistsMainUiDisabledForWasmActivation()
  {
    using var disabledServer = await TestWebServerBuilder.CreateTestServer(testOutput, settings: DisabledSettings());
    using var defaultServer = await TestWebServerBuilder.CreateTestServer(testOutput);

    // WASM seeds the gate from persisted component state before its first render, so a gated page is
    // never instantiated after activation. Without the persisted value the client would have to fetch
    // it, and would render the gated page body while that fetch was in flight.
    Assert.True(await GetPersistedMainUiDisabledAsync(disabledServer));
    Assert.False(await GetPersistedMainUiDisabledAsync(defaultServer));
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

  private static async Task<bool> GetPersistedMainUiDisabledAsync(TestWebServer testServer)
  {
    const string stateMarker = "Blazor-WebAssembly-Component-State:";
    using var client = await testServer.GetHttpClient();
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

    var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
    var stateStart = page.IndexOf(stateMarker, StringComparison.Ordinal) + stateMarker.Length;
    var stateEnd = page.IndexOf("-->", stateStart, StringComparison.Ordinal);
    var stateJson = Encoding.UTF8.GetString(Convert.FromBase64String(page[stateStart..stateEnd]));

    using var state = JsonDocument.Parse(stateJson);
    var encodedValue = state.RootElement.GetProperty("MainUiDisabled").GetString();
    using var decodedValue = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue!)));
    return decodedValue.RootElement.GetBoolean();
  }
}
