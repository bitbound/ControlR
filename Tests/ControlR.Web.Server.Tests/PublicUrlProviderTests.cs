using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins the contract of <see cref="IPublicUrlProvider"/>. The origin comes only from the configured
/// <c>AppOptions:PublicBaseUrl</c>, never from the request.
/// </summary>
/// <remarks>
/// End-to-end forged-header behavior is covered by <c>ForgedForwardedHostTests</c>.
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
  [InlineData("  https://controlr.test/\n", "https://controlr.test")]
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
    await using var testApp = await CreateApp(configured);
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    Assert.Null(provider.TryGetBaseUrl());
    Assert.Null(provider.TryGetAbsoluteUrl("Account/ResetPassword"));
    Assert.False(provider.HasTrustworthyOrigin);
  }

  [Fact]
  public async Task TryGetBaseUrl_ReturnsNull_WhenPublicBaseUrlIsNotSet()
  {
    await using var testApp = await CreateApp("");
    var provider = testApp.Services.GetRequiredService<IPublicUrlProvider>();

    Assert.Null(provider.TryGetBaseUrl());
    Assert.False(provider.HasTrustworthyOrigin);
  }

  private async Task<TestApp> CreateApp(string? publicBaseUrl = null)
  {
    var extraConfiguration = new Dictionary<string, string?>();

    if (publicBaseUrl is not null)
    {
      extraConfiguration["AppOptions:PublicBaseUrl"] = publicBaseUrl;
    }

    return await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration: extraConfiguration);
  }
}
