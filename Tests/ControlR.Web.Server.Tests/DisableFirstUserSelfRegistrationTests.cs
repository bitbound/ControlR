using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class DisableFirstUserSelfRegistrationTests(ITestOutputHelper testOutput)
{
  private const string AdminEmail = "admin@test.local";
  private const string AdminPassword = "FirstUserPass1!";

  [Fact]
  public async Task CreateUser_WhenBothRegistrationFlagsDisabledAndUsersExist_ReturnsRegistrationDisabledError()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration:
      TestConfigHelper.GetSelfRegistrationDisabledConfig(enablePublicRegistration: false));

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    await using var appDb = services.GetRequiredService<AppDb>();

    var tenant = await services.CreateTestTenant("Seed Tenant");
    await services.CreateTestUser(tenant.Id, email: "seed@existing.local");

    var userCreator = services.GetRequiredService<IUserCreator>();
    var result = await userCreator.CreateUser(
      "second@existing.local",
      AdminPassword,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(result.Succeeded);
    Assert.NotNull(result.IdentityResult);
    Assert.Single(result.IdentityResult.Errors);
    Assert.Equal("RegistrationDisabled", result.IdentityResult.Errors.First().Code);
  }

  [Fact]
  public async Task CreateUser_WhenFirstUserSelfRegistrationDisabledAndPublicRegistrationDisabled_ReturnsRegistrationDisabledError()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration:
      TestConfigHelper.GetSelfRegistrationDisabledConfig(enablePublicRegistration: false));

    using var scope = testApp.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

    var result = await userCreator.CreateUser(
      AdminEmail,
      AdminPassword,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(result.Succeeded);
    Assert.NotNull(result.IdentityResult);
    Assert.Single(result.IdentityResult.Errors);
    Assert.Equal("RegistrationDisabled", result.IdentityResult.Errors.First().Code);
  }

  [Fact]
  public async Task CreateUser_WhenFirstUserSelfRegistrationDisabledButPublicRegistrationEnabled_AllowsFirstUser()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration:
      TestConfigHelper.GetSelfRegistrationDisabledConfig(enablePublicRegistration: true));

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var userCreator = services.GetRequiredService<IUserCreator>();

    var result = await userCreator.CreateUser(
      AdminEmail,
      AdminPassword,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.Succeeded);
    Assert.NotNull(result.User);
  }

  [Fact]
  public async Task CreateUser_WhenFirstUserSelfRegistrationDisabled_DoesNotPromoteToServerAdministrator()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration:
      TestConfigHelper.GetSelfRegistrationDisabledConfig(enablePublicRegistration: true));

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var userCreator = services.GetRequiredService<IUserCreator>();

    var tenant = await services.CreateTestTenant("Test Tenant");
    var result = await userCreator.CreateUser(
      AdminEmail,
      AdminPassword,
      tenant.Id,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.Succeeded);
    Assert.NotNull(result.User);

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roles = await userManager.GetRolesAsync(result.User!);
    Assert.DoesNotContain(RoleNames.ServerAdministrator, roles);
  }

  [Fact]
  public async Task CreateUser_WhenFirstUserSelfRegistrationEnabledAndPublicRegistrationDisabled_AllowsFirstUser()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration:
      TestConfigHelper.GetSelfRegistrationEnabledConfig(enablePublicRegistration: false));

    using var scope = testApp.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

    var result = await userCreator.CreateUser(
      AdminEmail,
      AdminPassword,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.Succeeded);
    Assert.NotNull(result.User);
  }

  [Fact]
  public async Task CreateUser_WhenFirstUserSelfRegistrationEnabledAndServerIsEmpty_PromotesFirstUserToServerAdministrator()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration:
      TestConfigHelper.GetSelfRegistrationEnabledConfig(enablePublicRegistration: false));

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var userCreator = services.GetRequiredService<IUserCreator>();

    var result = await userCreator.CreateUser(
      AdminEmail,
      AdminPassword,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.Succeeded);
    Assert.NotNull(result.User);

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roles = await userManager.GetRolesAsync(result.User!);

    Assert.Contains(RoleNames.ServerAdministrator, roles);
    Assert.Contains(RoleNames.TenantAdministrator, roles);
  }

  [Fact]
  public async Task CreateUser_WhenFirstUserSelfRegistrationNotDisabledByDefault_AllowsFirstUser()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput, extraConfiguration: TestConfigHelper.GetBaseConfig());

    using var scope = testApp.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

    var result = await userCreator.CreateUser(
      AdminEmail,
      AdminPassword,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.Succeeded);
    Assert.NotNull(result.User);
  }
}