using System.Net;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Startup;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class BootstrapAdminUserTests(ITestOutputHelper output)
{
  private const string AdminEmail = "admin@bootstrap.test";
  private const string AdminPassword = "BootstrapPass1!";
  private const string PatSecret = "test-pat-secret-key-123456789012";
  private const string PatTokenId = "11111111-2222-3333-4444-555555555555";

  [Fact]
  public async Task Bootstrap_CreatesUserAndAssignsRolesAndConfirmsEmail()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = AdminPassword,
      ["AppOptions:DisableEmailSending"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);

    Assert.NotNull(user);
    Assert.Equal(AdminEmail, user.UserName);
    Assert.NotEqual(Guid.Empty, user.TenantId);
    Assert.True(user.EmailConfirmed);

    var roles = await userManager.GetRolesAsync(user);
    Assert.Contains(RoleNames.ServerAdministrator, roles);
    Assert.Contains(RoleNames.TenantAdministrator, roles);
    Assert.Contains(RoleNames.DeviceSuperUser, roles);
    Assert.Contains(RoleNames.AgentInstaller, roles);
    Assert.Contains(RoleNames.InstallerKeyManager, roles);

    var claims = await userManager.GetClaimsAsync(user);
    Assert.Contains(claims, c => c.Type == UserClaimTypes.UserId && c.Value == user.Id.ToString());
    Assert.Contains(claims, c => c.Type == UserClaimTypes.TenantId && c.Value == user.TenantId.ToString());
  }

  [Fact]
  public async Task Bootstrap_WhenUsersExist_Skips()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = AdminPassword,
      ["AppOptions:DisableEmailSending"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);

    // Create a user first so bootstrap should skip
    using (var preScope = testApp.CreateScope())
    {
      var userCreator = preScope.ServiceProvider.GetRequiredService<IUserCreator>();
      var preResult = await userCreator.CreateUser("existing@test.com", "ExistingPass1!", null,
        cancellationToken: TestContext.Current.CancellationToken);
      Assert.True(preResult.Succeeded);
    }

    // Now bootstrap should skip since users exist
    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var bootstrapUser = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);
    Assert.Null(bootstrapUser);
  }

  [Fact]
  public async Task Bootstrap_WithInMemoryDatabase_CreatesUser()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = AdminPassword,
      ["AppOptions:DisableEmailSending"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(
      output,
      extraConfiguration: config,
      useInMemoryDatabase: true);

    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);

    Assert.NotNull(user);
    Assert.NotEqual(Guid.Empty, user.TenantId);
    Assert.True(user.EmailConfirmed);
  }

  [Fact]
  public async Task Bootstrap_WithInvalidPassword_DoesNotCrash()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = "short"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);

    // Should not throw despite password policy violation
    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);
    Assert.Null(user);
  }

  [Fact]
  public async Task Bootstrap_WithNoOptions_DoesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(output);
    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var userCount = await appDb.Users.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, userCount);
  }

  [Fact]
  public async Task Bootstrap_WithPartialOptions_Throws()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
    await Assert.ThrowsAsync<InvalidOperationException>(
      () => testApp.App.BootstrapAdminUser());
  }

  [Fact]
  public async Task Bootstrap_WithPasswordOnly_Throws()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminPassword"] = AdminPassword
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
    await Assert.ThrowsAsync<InvalidOperationException>(
      () => testApp.App.BootstrapAdminUser());
  }

  [Fact]
  public async Task Bootstrap_WithPatSecretOnly_SkipsPatCreation()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = AdminPassword,
      ["Bootstrap:AdminPatSecret"] = PatSecret,
      ["AppOptions:DisableEmailSending"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);
    Assert.NotNull(user);

    var patCount = await appDb.PersonalAccessTokens
      .IgnoreQueryFilters()
      .Where(t => t.UserId == user.Id)
      .CountAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, patCount);
  }

  [Fact]
  public async Task Bootstrap_WithPatSecret_CreatesPat()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = AdminPassword,
      ["Bootstrap:AdminPatTokenId"] = PatTokenId,
      ["Bootstrap:AdminPatSecret"] = PatSecret,
      ["AppOptions:DisableEmailSending"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);
    Assert.NotNull(user);

    var tokens = await appDb.PersonalAccessTokens
      .IgnoreQueryFilters()
      .Where(t => t.UserId == user.Id && t.Name == "Bootstrap Admin PAT")
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Single(tokens);
    var token = tokens[0];
    Assert.NotEqual(Guid.Empty, token.Id);
    Assert.NotEmpty(token.HashedKey);

    var patManager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var fullToken = $"{Convert.ToHexString(token.Id.ToByteArray())}:{PatSecret}";
    var validationResult = await patManager.ValidateToken(fullToken);
    Assert.True(validationResult.IsSuccess);
    Assert.Equal(user.Id, validationResult.Value!.UserId);
  }

  [Fact]
  public async Task Bootstrap_WithPatSecret_PatCanAuthenticateApi()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = AdminPassword,
      ["Bootstrap:AdminPatTokenId"] = PatTokenId,
      ["Bootstrap:AdminPatSecret"] = PatSecret,
      ["AppOptions:DisableEmailSending"] = "true"
    };

    using var testServer = await TestWebServerBuilder.CreateTestServer(
      output,
      testDatabaseName: "BootstrapPatAuth",
      settings: config);

    using var scope = testServer.Services.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);
    Assert.NotNull(user);

    var token = await appDb.PersonalAccessTokens
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(t => t.Name == "Bootstrap Admin PAT" && t.UserId == user.Id, TestContext.Current.CancellationToken);
    Assert.NotNull(token);

    var fullToken = $"{Convert.ToHexString(token.Id.ToByteArray())}:{PatSecret}";

    var client = await testServer.GetHttpClient();
    client.DefaultRequestHeaders.Remove(PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName);
    client.DefaultRequestHeaders.Add(PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName, fullToken);

    var response = await client.GetAsync(HttpConstants.Internal.UsersEndpoint, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Bootstrap_WithPatSecret_TokenFormatIsValid()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:AdminEmail"] = AdminEmail,
      ["Bootstrap:AdminPassword"] = AdminPassword,
      ["Bootstrap:AdminPatTokenId"] = PatTokenId,
      ["Bootstrap:AdminPatSecret"] = PatSecret,
      ["AppOptions:DisableEmailSending"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var user = await appDb.Users
      .Include(u => u.PersonalAccessTokens)
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);

    Assert.NotNull(user);
    var pat = user!.PersonalAccessTokens!.First();

    // The token format should be {hex-guid}:{secret}
    var hexId = Convert.ToHexString(pat.Id.ToByteArray());
    Assert.Equal(32, hexId.Length); // 16 bytes = 32 hex chars
    Assert.NotEqual(Guid.Empty, pat.Id);
  }
}
