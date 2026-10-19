using System.Net;
using System.Text.RegularExpressions;
using ControlR.Libraries.Api.Contracts.Authz;
using ControlR.Web.Server.Tests.Helpers;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins that a statically rendered page sees the same permission grants as an interactive one.
/// </summary>
/// <remarks>
/// <para>
/// Identity pages render server-side on purpose, so Blazor WebAssembly never loads and the app shell is
/// built from the server principal. The nav's tenant and server admin sections are gated on
/// <c>controlr:client-policy</c> claims, which are a server-side decision rather than stored Identity
/// claims. These tests go through the real pipeline because that claim set is only present or absent
/// once a whole request has been authenticated and rendered.
/// </para>
/// <para>
/// The plain-user test is what stops the section from being fixed by simply always rendering it.
/// </para>
/// </remarks>
public class ServerRenderedNavVisibilityTests(ITestOutputHelper testOutput)
{
  private const string NavChromeMarker = "mud-navmenu";
  private const string Password = "T3stP@ssw0rd!";
  private const string TenantAdminHeading = "Tenant Admin";
  private const string TenantServiceAccountsLink = "Tenant Service Accounts";

  private static readonly Regex HiddenTypeRegex = new(@"type\s*=\s*""hidden""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex InputTagRegex = new(@"<input([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex NameAttributeRegex = new(@"name\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex ValueAttributeRegex = new(@"value\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

  [Fact]
  public async Task AccountManage_OmitsTenantAdminSection_ForPlainUser()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    // The first account in a fresh app is promoted to server administrator, so seed a throwaway
    // to leave the subject's grants coming only from the presets named below.
    await testServer.Services.CreateTestUser(tenant.Id, $"seed-{Guid.NewGuid():N}@t.local");
    var email = $"plain-{Guid.NewGuid():N}@t.local";
    await testServer.Services.CreateTestUser(tenant.Id, email);

    using var client = await SignInAsync(testServer, email);
    var page = await client.GetStringAsync("/Account/Manage", TestContext.Current.CancellationToken);

    Assert.Multiple(
      () => Assert.Contains(NavChromeMarker, page, StringComparison.Ordinal),
      () => Assert.Contains("Account", page, StringComparison.Ordinal),
      () => Assert.DoesNotContain(TenantAdminHeading, page, StringComparison.Ordinal),
      () => Assert.DoesNotContain(TenantServiceAccountsLink, page, StringComparison.Ordinal));
  }

  [Fact]
  public async Task AccountManage_ShowsTenantAdminSection_ForTenantAdministrator()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, $"seed-{Guid.NewGuid():N}@t.local");
    var email = $"admin-{Guid.NewGuid():N}@t.local";
    await testServer.Services.CreateTestUser(tenant.Id, email, PermissionPresets.TenantAdministrator);

    using var client = await SignInAsync(testServer, email);
    var page = await client.GetStringAsync("/Account/Manage", TestContext.Current.CancellationToken);

    Assert.Multiple(
      () => Assert.Contains(TenantAdminHeading, page, StringComparison.Ordinal),
      () => Assert.Contains(TenantServiceAccountsLink, page, StringComparison.Ordinal));
  }

  private static IEnumerable<KeyValuePair<string, string>> HiddenInputs(string html)
  {
    foreach (Match tag in InputTagRegex.Matches(html))
    {
      var attributes = tag.Groups[1].Value;
      if (!HiddenTypeRegex.IsMatch(attributes))
      {
        continue;
      }

      var name = NameAttributeRegex.Match(attributes);
      if (!name.Success)
      {
        continue;
      }

      var value = ValueAttributeRegex.Match(attributes);
      yield return new(name.Groups[1].Value, value.Success ? value.Groups[1].Value : string.Empty);
    }
  }

  /// <summary>
  /// Signs in through the real login form so the cookie this client carries is the one a browser would
  /// carry. The antiforgery field and the form's handler field are scraped from the rendered page.
  /// </summary>
  private static async Task<HttpClient> SignInAsync(TestWebServer testServer, string email)
  {
    var client = testServer.Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false,
      HandleCookies = true,
    });

    var loginPage = await client.GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);
    var form = new List<KeyValuePair<string, string>>(
    [
      new("Input.Email", email),
      new("Input.Password", Password),
    ]);
    form.AddRange(HiddenInputs(loginPage));

    var response = await client.PostAsync(
      "/Account/Login",
      new FormUrlEncodedContent(form),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.DoesNotContain("Account/Login", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
    return client;
  }
}
