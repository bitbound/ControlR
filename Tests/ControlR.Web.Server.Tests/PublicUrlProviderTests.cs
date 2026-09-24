using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins the contract of <see cref="IPublicUrlProvider"/> for the cases a configured base URL settles
/// without a request, which the pipeline tests cannot reach.
/// </summary>
/// <remarks>
/// Ref: https://github.com/bitbound/ControlR/issues/175
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

  [Theory]
  [InlineData("https://controlr.test", "https://controlr.test")]
  [InlineData("https://controlr.test/", "https://controlr.test")]
  [InlineData("https://controlr.test///", "https://controlr.test")]
  [InlineData("http://controlr.test:8443", "http://controlr.test:8443")]
  [InlineData("https://controlr.test/controlr", "https://controlr.test/controlr")]
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
  public async Task TryGetBaseUrl_ReturnsNull_WhenConfiguredBaseUrlIsNotAnHttpOrigin(string configured)
  {
    // A base URL that is not a plain http(s) origin is a misconfiguration. Falling back to the request
    // would silently undo the point of the setting, so nothing is produced instead.
    await using var testApp = await CreateApp(configured);
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    Assert.Null(provider.TryGetBaseUrl());
    Assert.Null(provider.TryGetAbsoluteUrl("Account/ResetPassword"));
  }

  private async Task<TestApp> CreateApp(string publicBaseUrl)
  {
    return await TestAppBuilder.CreateTestApp(
      testOutput,
      extraConfiguration: new Dictionary<string, string?>
      {
        ["AppOptions:PublicBaseUrl"] = publicBaseUrl,
      });
  }
}
