using System.Net;
using System.Text.RegularExpressions;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins that a caller cannot choose the origin of the links and redirects this server produces.
/// </summary>
/// <remarks>
/// <para>
/// Everything here goes through the real request pipeline via <c>WebApplicationFactory</c>, because the
/// defect lives in the forwarded-headers middleware. The controller-unit tests hand-build a
/// <c>DefaultHttpContext</c> and would pass no matter what the pipeline does.
/// </para>
/// <para>
/// <c>EnableNetworkTrust</c> is switched on deliberately. TestServer's connection is an unknown proxy,
/// and ASP.NET Core ignores <c>X-Forwarded-*</c> from one, so without it the forged header would be
/// dropped before reaching anything ControlR owns and every test below would pass while the bug was
/// still present. NetworkTrust clears the trusted lists, which is what makes a Cloudflare-fronted origin
/// behave the same way in production.
/// </para>
/// <para>
/// Ref: https://github.com/bitbound/ControlR/issues/175
/// </para>
/// </remarks>
public partial class ForgedForwardedHostTests(ITestOutputHelper testOutput)
{
  private const string ConfiguredOrigin = "https://controlr.test";
  private const string ForgedHost = "evil.example.com";

  [Fact]
  public async Task EmailedResetHasNoLink_WhenAllowedHostsIsPinnedButNoPublicBaseUrlIsSet()
  {
    var sender = new CapturingEmailSender();
    var settings = NewSettings();
    settings["AllowedHosts"] = "localhost";

    using var testServer = await TestWebServerBuilder.CreateTestServer(
      testOutput, settings: settings, configureServices: services => services.UseSender(sender));

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, "pinned@t.local");

    var page = await (await testServer.GetHttpClient()).GetStringAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);

    // A pinned AllowedHosts entry no longer supplies an origin for emailed links. Only PublicBaseUrl does.
    Assert.Contains(
      "Password reset emails cannot be sent from this server",
      page,
      StringComparison.Ordinal);
    Assert.True(
      string.IsNullOrEmpty(sender.Body),
      $"Expected no email at all, got: {sender.Body}");
  }

  [Fact]
  public async Task EmailedResetHasNoLink_WhenNoTrustworthyOriginExists()
  {
    var sender = new CapturingEmailSender();

    // AllowedHosts stays the shipped "*", and no public base URL is configured, so the host of an
    // arriving request is whatever the caller says it is. Nothing safe can be emailed, and a bare reset
    // code would be inert anyway: Account/ResetPassword takes the code only from the query string and
    // redirects to Account/InvalidPasswordReset without one. So the page refuses the action outright.
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      testOutput, settings: NewSettings(), configureServices: services => services.UseSender(sender));

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, "failclosed@t.local");

    var page = await (await testServer.GetHttpClient()).GetStringAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);

    Assert.Contains(
      "Password reset emails cannot be sent from this server",
      page,
      StringComparison.Ordinal);
    Assert.DoesNotContain("name=\"Input.Email\"", page, StringComparison.Ordinal);
    Assert.True(
      string.IsNullOrEmpty(sender.Body),
      $"Expected no email at all, got: {sender.Body}");
  }

  [Fact]
  public async Task EmailedResetLink_UsesConfiguredBaseUrl_WhenForwardedHostIsForged()
  {
    var sender = new CapturingEmailSender();
    var settings = NewSettings();
    settings["AppOptions:PublicBaseUrl"] = ConfiguredOrigin;

    using var testServer = await TestWebServerBuilder.CreateTestServer(
      testOutput, settings: settings, configureServices: services => services.UseSender(sender));

    await PostForgotPasswordAsync(testServer, "configured@t.local");

    AssertNoForgedHost(sender.ResetLink);
    Assert.True(
      sender.ResetLink.StartsWith($"{ConfiguredOrigin}/Account/ResetPassword?", StringComparison.Ordinal),
      $"Expected the configured origin, got: {sender.ResetLink}");
  }

  [Fact]
  public async Task PostSubmitRedirect_DoesNotPointAtForgedForwardedHost()
  {
    // Needs a trustworthy origin, or the form is not rendered at all and there is no submit to follow.
    var settings = NewSettings();
    settings["AppOptions:PublicBaseUrl"] = ConfiguredOrigin;

    using var testServer = await TestWebServerBuilder.CreateTestServer(
      testOutput,
      settings: settings,
      configureServices: services => services.UseSender(new CapturingEmailSender()));

    var response = await PostForgotPasswordAsync(testServer, "redirect@t.local");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    var location = response.Headers.Location?.ToString();
    Assert.NotNull(location);
    AssertNoForgedHost(location);
    Assert.Contains("Account/ForgotPasswordConfirmation", location);
  }

  [GeneratedRegex("https?://[^'\"\\s<]+")]
  private static partial Regex AbsoluteUrlRegex();

  private static void AssertNoForgedHost(string? actual)
  {
    Assert.True(
      actual is null || !actual.Contains(ForgedHost, StringComparison.OrdinalIgnoreCase),
      $"Expected no trace of the forged host, found it in: {actual}");
  }

  [GeneratedRegex(
    "<input[^>]*type=\"hidden\"[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"",
    RegexOptions.IgnoreCase | RegexOptions.Singleline)]
  private static partial Regex HiddenInputRegex();

  private static Dictionary<string, string?> NewSettings() => new()
  {
    // Emails have to actually reach the sender for a captured link to exist.
    ["AppOptions:DisableEmailSending"] = "false",
    ["AppOptions:EnableNetworkTrust"] = "true",
    // Cleared so the no-origin tests really have no origin. Tests that need one set it explicitly.
    ["AppOptions:PublicBaseUrl"] = "",
  };

  /// <summary>
  /// Reads the form so its antiforgery field is issued to this client, then posts it carrying a forged
  /// <c>X-Forwarded-Host</c>.
  /// </summary>
  private async Task<HttpResponseMessage> PostForgotPasswordAsync(TestWebServer testServer, string email)
  {
    // The reset email is only built for an existing user with a confirmed address. Without this the send
    // path returns early and every assertion below would pass while nothing was sent.
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email);

    // TestServer's own client has no cookie jar, so the antiforgery cookie issued by the GET below would
    // never come back and every POST would be rejected with a bodiless 400. Redirects are off so the
    // Location header stays observable.
    using var client = testServer.Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false,
      HandleCookies = true,
    });

    var page = await client.GetStringAsync("/Account/ForgotPassword", TestContext.Current.CancellationToken);

    var form = new List<KeyValuePair<string, string>>([new("Input.Email", email)]);
    foreach (Match match in HiddenInputRegex().Matches(page))
    {
      form.Add(new KeyValuePair<string, string>(match.Groups[1].Value, match.Groups[2].Value));
    }

    using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/ForgotPassword")
    {
      Content = new FormUrlEncodedContent(form),
    };
    request.Headers.TryAddWithoutValidation("X-Forwarded-Host", ForgedHost);

    return await client.SendAsync(request);
  }

  /// <summary>
  /// Stands in for SMTP, holding on to the last message so the tests can read the link it carried.
  /// </summary>
  private sealed class CapturingEmailSender : IEmailSender
  {
    private readonly List<string> _resetLinks = [];

    public string Body { get; private set; } = string.Empty;

    public string ResetLink => _resetLinks.FirstOrDefault() ?? string.Empty;

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
      Body = htmlMessage;

      foreach (Match match in AbsoluteUrlRegex().Matches(htmlMessage))
      {
        if (match.Value.Contains("ResetPassword", StringComparison.OrdinalIgnoreCase))
        {
          _resetLinks.Add(match.Value);
        }
      }

      return Task.CompletedTask;
    }
  }
}

file static class ServiceCollectionEmailSenderExtensions
{
  public static void UseSender(this IServiceCollection services, IEmailSender sender)
  {
    services.Replace(ServiceDescriptor.Singleton(sender));
  }
}
