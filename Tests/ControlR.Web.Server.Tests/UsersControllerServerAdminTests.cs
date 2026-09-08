using ControlR.Web.Server.Api.Internal;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class UsersControllerServerAdminTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task NonServerAdmin_CannotCreate_ServerAdministratorPreset()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    // Create a tenant admin caller (not a server admin)
    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      presets: PermissionPresets.TenantAdministrator);

    var request = new InternalDtos.CreateUserRequestDto(
      UserName: "evil",
      Email: "evil@t.local",
      Password: "P@ssw0rd!",
      PresetNames: [PermissionPresets.ServerAdministrator]);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    // Forbid translates to ForbidResult
    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task ServerAdmin_CanCreate_ServerAdministratorPreset()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    // Create a server admin caller
    var (controller, _, _) = await scope.CreateControllerWithTestData<UsersController>(
      presets: PermissionPresets.ServerAdministrator);

    await using var db = services.GetRequiredService<AppDb>();

    var request = new InternalDtos.CreateUserRequestDto(
      UserName: "super",
      Email: "super@t.local",
      Password: "P@ssw0rd!",
      PresetNames: [PermissionPresets.ServerAdministrator]);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    Assert.IsType<CreatedAtActionResult>(result.Result);

    var createdUser = await db.Users
      .FirstOrDefaultAsync(u => u.Email == "super@t.local", TestContext.Current.CancellationToken);
    Assert.NotNull(createdUser);

    var hasServerAdmin = await db.PermissionAssignments.AnyAsync(
      x => x.PrincipalId == createdUser.Id && x.PermissionName == PermissionNames.ServerPermissionsWrite,
      TestContext.Current.CancellationToken);
    Assert.True(hasServerAdmin);
  }

  [Fact]
  public async Task TenantPermissionManager_CanCreate_TenantPermissionPreset()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, tenant, caller) = await scope.CreateControllerWithTestData<UsersController>(
      presets: []);

    await using var db = services.GetRequiredService<AppDb>();
    db.PermissionAssignments.AddRange(
      PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        caller.Id,
        PermissionNames.TenantUsersWrite,
        PermissionScopeKind.Tenant,
        tenant.Id,
        tenant.Id,
        new PrincipalDescriptor(PrincipalType.User, caller.Id, tenant.Id, "test")),
      PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        caller.Id,
        PermissionNames.TenantPermissionsWrite,
        PermissionScopeKind.Tenant,
        tenant.Id,
        tenant.Id,
        new PrincipalDescriptor(PrincipalType.User, caller.Id, tenant.Id, "test")));
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var request = new InternalDtos.CreateUserRequestDto(
      UserName: "delegated",
      Email: "delegated@t.local",
      Password: "P@ssw0rd!",
      PresetNames: [PermissionPresets.DeviceSuperUser]);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    Assert.IsType<CreatedAtActionResult>(result.Result);

    var createdUser = await db.Users.FirstOrDefaultAsync(
      u => u.Email == "delegated@t.local", TestContext.Current.CancellationToken);
    Assert.NotNull(createdUser);
  }

  [Theory]
  [InlineData(PermissionPresets.AgentInstaller)]
  [InlineData(PermissionPresets.DeviceSuperUser)]
  [InlineData(PermissionPresets.InstallerKeyManager)]
  [InlineData(PermissionPresets.ServiceAccountManager)]
  [InlineData(PermissionPresets.TenantAdministrator)]
  public async Task TenantUsersWriteOnly_CannotCreate_TenantPermissionPreset(string presetName)
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controller, tenant, caller) = await scope.CreateControllerWithTestData<UsersController>(
      presets: []);

    await using var db = services.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      caller.Id,
      PermissionNames.TenantUsersWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, caller.Id, tenant.Id, "test")));
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var request = new InternalDtos.CreateUserRequestDto(
      UserName: "escalation",
      Email: "escalation@t.local",
      Password: "P@ssw0rd!",
      PresetNames: [presetName]);

    var result = await controller.Create(
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IUserCreator>(),
      request);

    Assert.IsType<ForbidResult>(result.Result);

    var createdUser = await db.Users.FirstOrDefaultAsync(
      u => u.Email == "escalation@t.local", TestContext.Current.CancellationToken);
    Assert.Null(createdUser);
  }
}
