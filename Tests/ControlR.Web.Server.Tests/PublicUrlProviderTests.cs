using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins the contract of <see cref="IPublicUrlProvider"/> for configuration and explicit request-host cases.
/// </summary>
/// <remarks>
/// <para>
/// These tests set the request host directly and do not run host-filtering or forwarded-headers middleware.
/// End-to-end forwarded-header behavior is covered by <c>ForgedForwardedHostTests</c>.
/// </para>
/// <para>
/// Ref: https://github.com/bitbound/ControlR/issues/175
/// </para>
/// </remarks>
public class PublicUrlProviderTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task TryGetAbsoluteUrl_AppendsQueryParameters()
  {
    await using var testApp = await CreateApp("https://controlr.test");
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    var url = provider.TryGetAbsoluteUrl(
      "Account/ConfirmEmail",
      new Dictionary<string, string?> { ["userId"] = "42", ["code"] = "a b+c" });

    Assert.Equal("https://controlr.test/Account/ConfirmEmail?userId=42&code=a%20b%2Bc", url);
  }

  [Fact]
  public async Task TryGetAbsoluteUrl_ToleratesLeadingAndTrailingSlashesOnThePath()
  {
    await using var testApp = await CreateApp("https://controlr.test/");
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    Assert.Equal(
      "https://controlr.test/Account/ResetPassword",
      provider.TryGetAbsoluteUrl("/Account/ResetPassword"));
    Assert.Equal(
      "https://controlr.test/Account/ResetPassword",
      provider.TryGetAbsoluteUrl("Account/ResetPassword"));
  }

  [Fact]
  public async Task TryGetBaseUrl_KeepsThePort_WhenAPinnedHostArrivesWithOne()
  {
    // Host filtering compares hostnames and ignores ports, so "app.t.local:8443" satisfies a pin on
    // "app.t.local". The link has to keep the port, or it would not point at the server in front of the
    // user already.
    await using var testApp = await CreateApp(allowedHosts: "app.t.local");
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    PinRequestHost(testApp.Services, "app.t.local:8443");

    Assert.Equal("https://app.t.local:8443", provider.TryGetBaseUrl());
  }

  [Theory]
  [InlineData("https://controlr.test", "https://controlr.test")]
  [InlineData("https://controlr.test/", "https://controlr.test")]
  [InlineData("https://controlr.test///", "https://controlr.test")]
  [InlineData("http://controlr.test:8443", "http://controlr.test:8443")]
  public async Task TryGetBaseUrl_ReturnsConfiguredOrigin(string configured, string expected)
  {
    await using var testApp = await CreateApp(configured);
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    Assert.Equal(expected, provider.TryGetBaseUrl());
  }

  [Theory]
  [InlineData("controlr.test")]
  [InlineData("/controlr")]
  [InlineData("ftp://controlr.test")]
  [InlineData("javascript:alert(1)")]
  [InlineData("not a url")]
  [InlineData("https://controlr.test/controlr")]
  [InlineData("https://controlr.test/controlr/")]
  [InlineData("https://controlr.test?query=1")]
  [InlineData("https://controlr.test#fragment")]
  [InlineData("https://user@controlr.test")]
  public async Task TryGetBaseUrl_ReturnsNull_WhenConfiguredBaseUrlIsNotAnOrigin(string configured)
  {
    // An invalid configured value must not fall back to an otherwise trusted request host.
    await using var testApp = await CreateApp(configured, allowedHosts: "app.t.local");
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    PinRequestHost(testApp.Services, "app.t.local");

    Assert.Null(provider.TryGetBaseUrl());
    Assert.Null(provider.TryGetAbsoluteUrl("Account/ResetPassword"));
    Assert.False(provider.HasTrustworthyOrigin);
  }

  [Theory]
  [InlineData("app.t.local", "*.t.local")]
  [InlineData("anything.deeper.t.local", "*.t.local")]
  [InlineData("deeper.t.local", "[::]")]
  [InlineData("deeper.t.local", "0.0.0.0")]
  [InlineData("other.test", "app.t.local")]
  [InlineData("", "app.t.local")]
  public async Task TryGetBaseUrl_ReturnsNull_WhenRequestHostIsNotNamedLiterally(string requestHost, string allowedHosts)
  {
    // A subdomain wildcard is not a pin. Host filtering accepts any hostname matching "*.t.local", so the
    // leftmost labels are still the caller's to choose, and echoing them back would rebuild the exact
    // defect this provider exists to close. The top-level forms do the same thing one step wider, and an
    // absent host has nothing to offer at all.
    await using var testApp = await CreateApp(allowedHosts: allowedHosts);
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    PinRequestHost(testApp.Services, requestHost);

    Assert.Null(provider.TryGetBaseUrl());
    Assert.False(provider.HasTrustworthyOrigin);
  }

  [Fact]
  public async Task TryGetBaseUrl_ReturnsRequestOrigin_WhenRequestHostIsNamedLiterally()
  {
    // An operator who writes down the exact hostname has stated their own origin, so nothing an attacker
    // sends can be mistaken for it. This is the fallback that keeps a pinned deployment emailing clickable
    // links without a configured base URL.
    await using var testApp = await CreateApp(allowedHosts: "app.t.local");
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    PinRequestHost(testApp.Services, "app.t.local");

    Assert.Equal("https://app.t.local", provider.TryGetBaseUrl());
    Assert.True(provider.HasTrustworthyOrigin);
  }

  [Fact]
  public async Task TryGetBaseUrl_ReturnsRequestOrigin_WhenRequestHostMatchesOneOfSeveralPinnedHosts()
  {
    // The default host-filtering setup reads AllowedHosts as one semicolon-separated value, so a deployment
    // that answers to more than one hostname lists them that way. Each entry is matched on its own, so only
    // the one that arrived should count.
    await using var testApp = await CreateApp(allowedHosts: "other.test;app.t.local");
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    PinRequestHost(testApp.Services, "app.t.local");

    Assert.Equal("https://app.t.local", provider.TryGetBaseUrl());
  }

  /// <summary>
  /// Stands in for the request pipeline, which is what normally fills this in.
  /// </summary>
  private static void PinRequestHost(IServiceProvider services, string host)
  {
    services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
    {
      Request =
      {
        Scheme = Uri.UriSchemeHttps,
        Host = new HostString(host),
      },
    };
  }

  private async Task<TestApp> CreateApp(string? publicBaseUrl = null, string? allowedHosts = null)
  {
    var extraConfiguration = new Dictionary<string, string?>();

    if (publicBaseUrl is not null)
    {
      extraConfiguration["AppOptions:PublicBaseUrl"] = publicBaseUrl;
    }

    if (allowedHosts is not null)
    {
      extraConfiguration["AllowedHosts"] = allowedHosts;
    }

    return await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration: extraConfiguration);
  }
}
