using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Startup;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Verifies first-user scenarios against a real Postgres database, exercising
/// the database-constraint paths that in-memory tests cannot cover.
/// </summary>
public class FirstUserTests(ITestOutputHelper output)
{
  private const string AdminEmail = "admin@firstuser.test";
  private const string AdminPassword = "FirstUserPass1!";

  [Fact]
  public async Task Bootstrap_CreatesFirstUserWithAllRoles()
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
      useInMemoryDatabase: false);

    await testApp.App.BootstrapAdminUser();

    using var scope = testApp.CreateScope();
    var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
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
  }

  [Fact]
  public async Task PublicRegistration_CreatesFirstUserWithAllRoles()
  {
    var config = new Dictionary<string, string?>
    {
      ["AppOptions:DisableEmailSending"] = "true",
      ["AppOptions:EnablePublicRegistration"] = "true"
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(
      output,
      extraConfiguration: config,
      useInMemoryDatabase: false);

    using var scope = testApp.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

    var result = await userCreator.CreateUser(
      AdminEmail,
      AdminPassword,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.Succeeded);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = result.User;

    Assert.NotNull(user);
    Assert.Equal(AdminEmail, user.UserName);
    Assert.NotEqual(Guid.Empty, user.TenantId);

    var roles = await userManager.GetRolesAsync(user);
    Assert.Contains(RoleNames.ServerAdministrator, roles);
    Assert.Contains(RoleNames.TenantAdministrator, roles);
    Assert.Contains(RoleNames.DeviceSuperUser, roles);
    Assert.Contains(RoleNames.AgentInstaller, roles);
    Assert.Contains(RoleNames.InstallerKeyManager, roles);
  }
}
