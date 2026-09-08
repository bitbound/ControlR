using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Startup;
using ControlR.Web.Server.Tests.Helpers;
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
  public async Task Bootstrap_CreatesFirstUserWithAllPresets()
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
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Email == AdminEmail, TestContext.Current.CancellationToken);

    Assert.NotNull(user);
    Assert.Equal(AdminEmail, user.UserName);
    Assert.NotEqual(Guid.Empty, user.TenantId);
    Assert.True(user.EmailConfirmed);

    var permissions = await appDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);
    Assert.Contains(PermissionNames.ServerPermissionsWrite, permissions);
    Assert.Contains(PermissionNames.TenantSettingsWrite, permissions);
    Assert.Contains(PermissionNames.DeviceRead, permissions);
    Assert.Contains(PermissionNames.AgentInstall, permissions);
    Assert.Contains(PermissionNames.InstallerKeyRead, permissions);
  }

  [Fact]
  public async Task PublicRegistration_CreatesFirstUserWithAllPresets()
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

    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var user = result.User;

    Assert.NotNull(user);
    Assert.Equal(AdminEmail, user.UserName);
    Assert.NotEqual(Guid.Empty, user.TenantId);

    var permissions = await appDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);
    Assert.Contains(PermissionNames.ServerPermissionsWrite, permissions);
    Assert.Contains(PermissionNames.TenantSettingsWrite, permissions);
    Assert.Contains(PermissionNames.DeviceRead, permissions);
    Assert.Contains(PermissionNames.AgentInstall, permissions);
    Assert.Contains(PermissionNames.InstallerKeyRead, permissions);
  }
}
