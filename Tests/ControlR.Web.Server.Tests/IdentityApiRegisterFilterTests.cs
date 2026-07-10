using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class IdentityApiRegisterFilterTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task NonRegisterEndpoint_IsNotInterceptedByFilter()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    using var httpClient = await testServer.GetHttpClient();
    var loginRequest = new LoginRequest { Email = "nobody@test.local", Password = "nope" };

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/login",
      loginRequest,
      TestContext.Current.CancellationToken);

    // Should NOT be 404 (the filter didn't intercept it).
    // Expect 401 Unauthorized since credentials are bogus, or 400 for bad request.
    Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Register_FirstUser_WhenPublicRegistrationDisabled_StillSucceeds()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:EnablePublicRegistration"] = "false",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    // No pre-existing users — DB is empty. First-user registration should succeed.

    using var httpClient = await testServer.GetHttpClient();
    var request = new RegisterRequest { Email = "first@test.local", Password = "T3stP@ssw0rd!" };

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // Verify the first user received the expected roles.
    using var scope = testServer.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = await userManager.FindByEmailAsync(request.Email);
    Assert.NotNull(user);
    var roles = await userManager.GetRolesAsync(user);
    Assert.Contains(RoleNames.ServerAdministrator, roles);
    Assert.Contains(RoleNames.TenantAdministrator, roles);
    Assert.Contains(RoleNames.DeviceSuperUser, roles);
    Assert.Contains(RoleNames.AgentInstaller, roles);
    Assert.Contains(RoleNames.InstallerKeyManager, roles);
  }

  [Fact]
  public async Task Register_WhenDuplicateEmail_ReturnsValidationProblem()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:EnablePublicRegistration"] = "true",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    // First call succeeds.
    using var httpClient = await testServer.GetHttpClient();
    var request = new RegisterRequest { Email = "dupe@test.local", Password = "T3stP@ssw0rd!" };
    var firstResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      request,
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

    // Second call with same email should fail.
    var secondResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
  }

  [Fact]
  public async Task Register_WhenPasswordTooWeak_ReturnsValidationProblem()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:EnablePublicRegistration"] = "true",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    using var httpClient = await testServer.GetHttpClient();
    var request = new RegisterRequest { Email = "weakpw@test.local", Password = "short" };

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Register_WhenPublicRegistrationDisabled_SecondUserReturnsNotFound()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:EnablePublicRegistration"] = "false",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    // Register the first admin user through the API.
    using var httpClient = await testServer.GetHttpClient();
    var adminRequest = new RegisterRequest { Email = "admin@test.local", Password = "T3stP@ssw0rd!" };
    var adminResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      adminRequest,
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

    // Verify the first user got ServerAdministrator.
    using (var scope = testServer.Services.CreateScope())
    {
      var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
      var adminUser = await userManager.FindByEmailAsync(adminRequest.Email);
      Assert.NotNull(adminUser);
      var adminRoles = await userManager.GetRolesAsync(adminUser);
      Assert.Contains(RoleNames.ServerAdministrator, adminRoles);
    }

    // A second user should be blocked when public registration is disabled.
    var request = new RegisterRequest { Email = "second@test.local", Password = "T3stP@ssw0rd!" };
    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Register_WhenPublicRegistrationDisabled_WithExistingUser_ReturnsNotFound()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:EnablePublicRegistration"] = "false",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    // Pre-populate DB so it's not the first-user scenario.
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: "existing@test.local");

    using var httpClient = await testServer.GetHttpClient();
    var request = new RegisterRequest { Email = "new@test.local", Password = "T3stP@ssw0rd!" };

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Register_WhenPublicRegistrationEnabled_CreatesUserAndReturnsOk()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:EnablePublicRegistration"] = "true",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    // Pre-populate a first user so the register call creates a non-first user.
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: "existing@test.local");

    using var httpClient = await testServer.GetHttpClient();
    var request = new RegisterRequest { Email = "newuser@test.local", Password = "T3stP@ssw0rd!" };

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // Verify the non-first user does NOT get ServerAdministrator,
    // but does get tenant-scoped roles.
    using var scope = testServer.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = await userManager.FindByEmailAsync(request.Email);
    Assert.NotNull(user);
    var roles = await userManager.GetRolesAsync(user);
    Assert.DoesNotContain(RoleNames.ServerAdministrator, roles);
    Assert.Contains(RoleNames.TenantAdministrator, roles);
    Assert.Contains(RoleNames.DeviceSuperUser, roles);
    Assert.Contains(RoleNames.AgentInstaller, roles);
    Assert.Contains(RoleNames.InstallerKeyManager, roles);
  }

  [Fact]
  public async Task Register_WhenRequestBodyIsMissing_ReturnsProblem()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: new Dictionary<string, string?>
      {
        ["AppOptions:EnableInteractiveBearerLogin"] = "true",
        ["AppOptions:EnablePublicRegistration"] = "true",
        ["AppOptions:DisableEmailSending"] = "true",
      });

    using var httpClient = await testServer.GetHttpClient();

    // Send empty body (not valid JSON for RegisterRequest).
    using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
    var response = await httpClient.PostAsync(
      $"{HttpConstants.Internal.AuthEndpoint}/register",
      content,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
}
