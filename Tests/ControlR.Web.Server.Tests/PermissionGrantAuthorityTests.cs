using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.PermissionAssignments;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Grant-authority matrix: locks down who may grant what. Delegated administration (a
/// tenant writer may grant permissions they do not hold), the server.permissions.write requirement for
/// server-scoped grants, replace visibility semantics, and the policy-layer gate for
/// principals without tenant.permissions.write.
/// </summary>
public class PermissionGrantAuthorityTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task ApplyPresets_ReplaceExistingFalse_WithExistingDeny_SkipsAllow()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    // Seed a DENY for DeviceRead at Tenant scope on the target. ApplyPresets selects existing
    // keys by (PermissionName, ScopeKind) regardless of effect, so this deny must suppress the
    // preset's DeviceRead allow (fail-closed: the deny is preserved, no escalation).
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"),
      PermissionEffect.Deny));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.ApplyPresets(
      new InternalDtos.ApplyPermissionPresetsRequestDto(
        PermissionPrincipalKind.User,
        target.Id,
        [PermissionPresets.DeviceSuperUser],
        ReplaceExisting: false),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected preset application to succeed: {result.Reason}");

    // The preset should have skipped DeviceRead because a deny already occupies that key.
    var expectedGranted = PermissionPresets.GetPermissions(PermissionPresets.DeviceSuperUser)
      .Where(name => name != PermissionNames.DeviceRead)
      .Distinct()
      .Count();
    Assert.Equal(expectedGranted, result.Value);
  }

    [Fact]
    public async Task ApplyPresets_WithMixedScopesAndRequiredPermissions_Succeeds()
    {
      await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
      var tenant = await testApp.App.Services.CreateTestTenant();
      await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
      var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
      var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

      await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        actor.Id,
        PermissionNames.ServerPermissionsWrite,
        PermissionScopeKind.Server,
        null,
        tenant.Id,
        new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));
      await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        actor.Id,
        PermissionNames.TenantPermissionsWrite,
        PermissionScopeKind.Tenant,
        tenant.Id,
        tenant.Id,
        new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

      using var scope = testApp.CreateScope();
      var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

      var result = await manager.ApplyPresets(
        new InternalDtos.ApplyPermissionPresetsRequestDto(
          PermissionPrincipalKind.User,
          target.Id,
          [PermissionPresets.ServerAdministrator],
          ReplaceExisting: false),
        tenant.Id,
        Actor(actor.Id, tenant.Id),
        TestContext.Current.CancellationToken);

      Assert.True(result.IsSuccess, $"Expected preset application to succeed: {result.Reason}");
      Assert.Equal(PermissionPresets.GetPermissions(PermissionPresets.ServerAdministrator).Count, result.Value);
    }

  [Fact]
  public async Task CreateMany_ResourceScopedServerScope_ForUser_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.CreateMany(
      [ServerScopeDeviceReadRequest(target.Id)],
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
    Assert.Contains("server service accounts", result.Reason);
  }

  [Fact]
  public async Task CreateToken_ResourceScopedServerScope_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var owner = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"owner-{Guid.NewGuid():N}@t.local");

    // The seeded Server-scope owner grant does not matter here: the loader drops it for a
    // tenant-bound user, so the owner can never hold Server-scope reach. What stops this request
    // is the credential rule itself, not a comparison of ownership reach.
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      owner.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, owner.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var patManager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Cross-tenant PAT",
        PersonalAccessTokenPermissionMode.Restricted,
        [new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Server, null)]),
      owner.Id,
      Actor(owner.Id, tenant.Id));

    Assert.False(result.IsSuccess);
    Assert.Contains("reserved for server service accounts", result.Reason);
  }

  [Fact]
  public async Task CreateToken_ServerOnlyPermissionAtServerScope_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var owner = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"owner-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      owner.Id,
      PermissionNames.ServerAlertsRead,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, owner.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var patManager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Server admin PAT",
        PersonalAccessTokenPermissionMode.Restricted,
        [new InternalDtos.CredentialScopeDto(PermissionNames.ServerAlertsRead, PermissionScopeKind.Server, null)]),
      owner.Id,
      Actor(owner.Id, tenant.Id));

    Assert.True(result.IsSuccess, $"Expected PAT creation to succeed: {result.Reason}");
  }

  [Fact]
  public async Task Create_AssignmentTargetingServerServiceAccount_ByServerAdmin_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var setupScope = testApp.CreateScope();
    var accountManager = setupScope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var accountResult = await accountManager.CreateForServer(
      $"server-sa-{Guid.NewGuid():N}", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess);
    var accountId = accountResult.Value.Id;

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.ServiceAccount,
        accountId,
        PermissionNames.ServerAlertsRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected to succeed: {result.Reason}");
  }

  [Fact]
  public async Task Create_AssignmentTargetingServerServiceAccount_ByTenantAdmin_Forbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    // Tenant admin with TenantPermissionsWrite but no ServerPermissionsWrite. A server
    // service account is a cross-tenant principal, so targeting one must require server
    // write authority; a tenant-scoped grant must not be attachable to it.
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    // Create a server-scoped service account to target.
    using var setupScope = testApp.CreateScope();
    var accountManager = setupScope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var accountResult = await accountManager.CreateForServer(
      $"server-sa-{Guid.NewGuid():N}", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess);
    var accountId = accountResult.Value.Id;

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.ServiceAccount,
        accountId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenant.Id,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);
  }

  [Fact]
  public async Task Create_Deny_WithoutTenantPermissionsDeny_ReturnsForbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    // Actor holds TenantPermissionsWrite (can manage allow rules) but NOT TenantPermissionsDeny.
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();
    var denyRequest = new InternalDtos.CreatePermissionAssignmentRequestDto(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.DeviceRead,
      PermissionEffect.Deny,
      PermissionScopeKind.Tenant,
      tenant.Id,
      null);

    // A tenant writer without TenantPermissionsDeny must be forbidden from creating a deny rule.
    var denyResult = await manager.Create(
      denyRequest,
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);
    Assert.False(denyResult.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, denyResult.ErrorCode);

    // The same actor can still create an allow rule (TenantPermissionsWrite is sufficient).
    var allowResult = await manager.Create(
      denyRequest with { Effect = PermissionEffect.Allow },
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);
    Assert.True(allowResult.IsSuccess, $"Expected allow grant to succeed: {allowResult.Reason}");
  }

  [Fact]
  public async Task Create_Deny_WithTenantPermissionsDeny_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    // Actor holds both TenantPermissionsWrite and TenantPermissionsDeny.
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsDeny,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        target.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Deny,
        PermissionScopeKind.Tenant,
        tenant.Id,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected deny grant to succeed: {result.Reason}");
  }

  [Fact]
  public async Task Create_DeviceScopeWrongDevice_ForLogonToken_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var recipient = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"recipient-{Guid.NewGuid():N}@t.local");
    var tokenDevice = await testApp.App.Services.CreateTestDevice(tenant.Id);
    var otherDevice = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      recipient.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, recipient.Id, tenant.Id, "test")));

    var tokenId = Guid.NewGuid();
    await SeedLogonToken(testApp, tokenId, recipient.Id, tokenDevice.Id, tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    // A Device row for a different device survives every existing gate (the device exists in the
    // tenant, the owner holds the permission) but the loaders filter it out against the token's
    // device scope, so it is inert. It must be rejected at write time.
    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.LogonToken,
        tokenId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Device,
        otherDevice.Id,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
  }

  [Fact]
  public async Task Create_IdenticalAssignment_ReturnsConflict_WhileOppositeEffectSucceeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsDeny,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();
    var request = new InternalDtos.CreatePermissionAssignmentRequestDto(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.DeviceRead,
      PermissionEffect.Allow,
      PermissionScopeKind.Tenant,
      tenant.Id,
      null);

    var first = await manager.Create(request, tenant.Id, Actor(actor.Id, tenant.Id), TestContext.Current.CancellationToken);
    var duplicate = await manager.Create(request, tenant.Id, Actor(actor.Id, tenant.Id), TestContext.Current.CancellationToken);
    var deny = await manager.Create(
      request with { Effect = PermissionEffect.Deny },
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(first.IsSuccess);
    Assert.False(duplicate.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Conflict, duplicate.ErrorCode);
    Assert.True(deny.IsSuccess);
  }

  [Fact]
  public async Task Create_ResourceScopedServerScopeDeny_ForLogonToken_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var recipient = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"recipient-{Guid.NewGuid():N}@t.local");
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsDeny,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    var tokenId = Guid.NewGuid();
    await SeedLogonToken(testApp, tokenId, recipient.Id, device.Id, tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    // Logon-token rules are only ever loaded when they are Device-scoped to the token's device,
    // so any other scope kind is inert by construction. The write gate must reject it rather than
    // report success for a row that can never evaluate.
    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.LogonToken,
        tokenId,
        PermissionNames.DeviceRead,
        PermissionEffect.Deny,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
  }

  [Fact]
  public async Task Create_ResourceScopedServerScopeDeny_ForPersonalAccessToken_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var owner = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"owner-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsDeny,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    // Deny validation skips owner grant-authority entirely (a deny confers nothing, so there is
    // nothing for the owner to be able to grant), and the Server-scope rule is allow-only. The
    // owner's reach is therefore irrelevant: this row is accepted without any ownership check.
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      owner.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, owner.Id, tenant.Id, "test")));

    var tokenId = Guid.NewGuid();
    await SeedPersonalAccessToken(testApp, tokenId, owner.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.PersonalAccessToken,
        tokenId,
        PermissionNames.DeviceRead,
        PermissionEffect.Deny,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, result.Reason);
  }

  [Fact]
  public async Task Create_ResourceScopedServerScopeDeny_ForUser_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsDeny,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    // A deny narrows only its own principal, so it confers no cross-tenant reach and must stay
    // assignable. This is the org-wide kill switch for a tenant-bound principal.
    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        target.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Deny,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, result.Reason);
  }

  [Fact]
  public async Task Create_ResourceScopedServerScope_ForPersonalAccessToken_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var patOwner = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"pat-owner-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var setupScope = testApp.CreateScope();
    var patManager = setupScope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Test PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      patOwner.Id,
      Actor(patOwner.Id, tenant.Id));
    Assert.True(patResult.IsSuccess, $"Expected PAT creation to succeed: {patResult.Reason}");
    var patId = patResult.Value.PersonalAccessToken.Id;

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.PersonalAccessToken,
        patId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
    // Asserted on the reason: without it the owner's own missing reach at the server resource
    // would produce BadRequest too, and the test would pass even if the rule were removed.
    Assert.Contains("server service accounts", result.Reason);
  }

  [Fact]
  public async Task Create_ResourceScopedServerScope_ForServerServiceAccount_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var setupScope = testApp.CreateScope();
    var accountManager = setupScope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var accountResult = await accountManager.CreateForServer(
      $"server-sa-{Guid.NewGuid():N}", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess);
    var accountId = accountResult.Value.Id;

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.ServiceAccount,
        accountId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected server-SA device grant to succeed: {result.Reason}");
  }

  [Fact]
  public async Task Create_ResourceScopedServerScope_ForTenantServiceAccount_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var setupScope = testApp.CreateScope();
    var accountManager = setupScope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var accountResult = await accountManager.CreateForTenant(
      $"tenant-sa-{Guid.NewGuid():N}", null, tenant.Id,
      Actor(actor.Id, tenant.Id), TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess);
    var accountId = accountResult.Value.Id;

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.ServiceAccount,
        accountId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
  }

  [Fact]
  public async Task Create_ResourceScopedServerScope_ForUserSharingIdWithServerServiceAccount_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    // Principal identity is only unique within a PermissionPrincipalKind. A user whose id matches a
    // server account id must still be classified as tenant-bound.
    var sharedId = Guid.NewGuid();
    await SeedServerServiceAccountWithId(testApp, sharedId);
    await SeedUserWithId(testApp, sharedId, tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        sharedId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
    Assert.Contains("server service accounts", result.Reason);
  }

  [Fact]
  public async Task Create_ResourceScopedServerScope_ForUser_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        target.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
  }

  [Fact]
  public async Task Create_ServerScoped_ByNonServerAdmin_Forbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        target.Id,
        PermissionNames.ServerTenantsWrite,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
        Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
      Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);
  }

  [Fact]
  public async Task Create_ServerScoped_ByServerAdmin_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        target.Id,
        PermissionNames.ServerAlertsRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      tenant.Id,
        Actor(actor.Id, tenant.Id),
        TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected server-admin grant to succeed: {result.Reason}");
  }

  [Fact]
  public async Task Create_TenantScopedPermission_WithTenantPermissionsWrite_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        target.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenant.Id,
        null),
      tenant.Id,
        Actor(actor.Id, tenant.Id),
        TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected delegated-admin grant to succeed: {result.Reason}");
  }

  [Fact]
  public async Task Create_ViaApi_WithoutWritePermission_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(tenant.Id, $"no-write-{Guid.NewGuid():N}@t.local");

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Grant Authority Test PAT", PersonalAccessTokenPermissionMode.InheritOwner), user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess);
    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        user.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenant.Id,
        null),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Delete_ServerSA_Target_ByTenantAdmin_ReturnsForbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var setupScope = testApp.CreateScope();
    var accountManager = setupScope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var accountResult = await accountManager.CreateForServer(
      $"server-sa-{Guid.NewGuid():N}", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess);
    var accountId = accountResult.Value.Id;

    var saRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.ServiceAccount,
      accountId,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));
    await SeedAssignment(testApp, saRow);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var deleteResult = await manager.Delete(
      saRow.Id, tenant.Id, Actor(actor.Id, tenant.Id), TestContext.Current.CancellationToken);

    Assert.False(deleteResult.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, deleteResult.ErrorCode);

    var deleteManyResult = await manager.DeleteMany(
      [saRow.Id], tenant.Id, Actor(actor.Id, tenant.Id), TestContext.Current.CancellationToken);

    Assert.False(deleteManyResult.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, deleteManyResult.ErrorCode);
  }

  [Fact]
  public async Task ReplaceForPrincipal_ResourceScopedServerScope_ForUser_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.ReplaceForPrincipal(
      PermissionPrincipalKind.User,
      target.Id,
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      [ServerScopeDeviceReadRequest(target.Id)],
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
    Assert.Contains("server service accounts", result.Reason);
  }

  [Fact]
  public async Task Replace_ByServerAdmin_RewritesTenantAndServerRows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsRead,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    var tenantRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));

    var serverRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.ServerAlertsRead,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));

    await SeedAssignment(testApp, tenantRow);
    await SeedAssignment(testApp, serverRow);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var replaceResult = await manager.ReplaceForPrincipal(
      PermissionPrincipalKind.User,
      target.Id,
      tenant.Id,
        Actor(actor.Id, tenant.Id),
      [
        new InternalDtos.CreatePermissionAssignmentRequestDto(
          PermissionPrincipalKind.User,
          target.Id,
          PermissionNames.DeviceLogsRead,
          PermissionEffect.Allow,
          PermissionScopeKind.Tenant,
          tenant.Id,
          null)
      ],
        TestContext.Current.CancellationToken);

    Assert.True(replaceResult.IsSuccess, $"Expected replace to succeed: {replaceResult.Reason}");

    var remaining = await manager.GetByPrincipal(
      PermissionPrincipalKind.User,
      target.Id,
      tenant.Id,
        Actor(actor.Id, tenant.Id),
        TestContext.Current.CancellationToken);

    Assert.Equal(2, remaining.Count);
    Assert.Contains(remaining, assignment => assignment.PermissionName == PermissionNames.DeviceLogsRead);
    Assert.Contains(remaining, assignment => assignment.PermissionName == PermissionNames.ServerAlertsRead);
  }

  [Fact]
  public async Task Replace_ByServerAdmin_WithServerScopedAssignments_PreservesTenantRows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsRead,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    var tenantRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));
    var serverRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.ServerAlertsRead,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));

    await SeedAssignment(testApp, tenantRow);
    await SeedAssignment(testApp, serverRow);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var replaceResult = await manager.ReplaceForPrincipal(
      PermissionPrincipalKind.User,
      target.Id,
      tenant.Id,
        Actor(actor.Id, tenant.Id),
      [
        new InternalDtos.CreatePermissionAssignmentRequestDto(
          PermissionPrincipalKind.User,
          target.Id,
          PermissionNames.ServerTelemetryRead,
          PermissionEffect.Allow,
          PermissionScopeKind.Server,
          null,
          null)
      ],
        TestContext.Current.CancellationToken);

    Assert.True(replaceResult.IsSuccess, $"Expected replace to succeed: {replaceResult.Reason}");

    var remaining = await manager.GetByPrincipal(
      PermissionPrincipalKind.User,
      target.Id,
      tenant.Id,
        Actor(actor.Id, tenant.Id),
        TestContext.Current.CancellationToken);

    Assert.Contains(remaining, x => x.Id == tenantRow.Id && x.PermissionName == PermissionNames.DeviceRead);
    Assert.DoesNotContain(remaining, x => x.Id == serverRow.Id);
    Assert.Contains(remaining, x => x.PermissionName == PermissionNames.ServerTelemetryRead);
  }

  [Fact]
  public async Task Update_ResourceScopedServerScope_ForServerServiceAccount_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var setupScope = testApp.CreateScope();
    var accountManager = setupScope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var accountResult = await accountManager.CreateForServer(
      $"server-sa-{Guid.NewGuid():N}", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess);
    var accountId = accountResult.Value.Id;

    var saRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.ServiceAccount,
      accountId,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));
    await SeedAssignment(testApp, saRow);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Update(
      saRow.Id,
      new InternalDtos.UpdatePermissionAssignmentRequestDto(
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        "updated notes",
        true),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected server-SA update to succeed: {result.Reason}");
  }

  [Fact]
  public async Task Update_ResourceScopedToServerScope_ForUser_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server,
      null,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));
    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    var userRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));
    await SeedAssignment(testApp, userRow);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Update(
      userRow.Id,
      new InternalDtos.UpdatePermissionAssignmentRequestDto(
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null,
        true),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
  }

  [Fact]
  public async Task Update_ServerSA_Target_ByTenantAdmin_ReturnsForbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      actor.Id,
      PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

    using var setupScope = testApp.CreateScope();
    var accountManager = setupScope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var accountResult = await accountManager.CreateForServer(
      $"server-sa-{Guid.NewGuid():N}", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(accountResult.IsSuccess);
    var accountId = accountResult.Value.Id;

    var saRow = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.ServiceAccount,
      accountId,
      PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));
    await SeedAssignment(testApp, saRow);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Update(
      saRow.Id,
      new InternalDtos.UpdatePermissionAssignmentRequestDto(
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenant.Id,
        "modified by tenant admin",
        true),
      tenant.Id,
      Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);
  }

    [Fact]
    public async Task Update_ServerScopeToTenantScope_WithoutTenantPermissionsWrite_Forbidden()
    {
      await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
      var tenant = await testApp.App.Services.CreateTestTenant();
      await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
      var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
      var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

      await SeedAssignment(testApp, PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        actor.Id,
        PermissionNames.ServerPermissionsWrite,
        PermissionScopeKind.Server,
        null,
        tenant.Id,
        new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test")));

      var assignment = PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        target.Id,
        PermissionNames.ServerAlertsRead,
        PermissionScopeKind.Server,
        null,
        tenant.Id,
        new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));
      await SeedAssignment(testApp, assignment);

      using var scope = testApp.CreateScope();
      var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

      var result = await manager.Update(
        assignment.Id,
        new InternalDtos.UpdatePermissionAssignmentRequestDto(
          PermissionNames.DeviceRead,
          PermissionEffect.Allow,
          PermissionScopeKind.Tenant,
          tenant.Id,
          null,
          true),
        tenant.Id,
        Actor(actor.Id, tenant.Id),
        TestContext.Current.CancellationToken);

      Assert.False(result.IsSuccess);
      Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);
    }

  [Fact]
  public async Task Update_ToServerScope_ByNonServerAdmin_Forbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    var target = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local");

    var assignment = PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      target.Id,
      PermissionNames.ServerAlertsRead,
      PermissionScopeKind.Tenant,
      tenant.Id,
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"));

    await SeedAssignment(testApp, assignment);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Update(
      assignment.Id,
      new InternalDtos.UpdatePermissionAssignmentRequestDto(
        PermissionNames.ServerAlertsRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null,
        true),
      tenant.Id,
        Actor(actor.Id, tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
      Assert.Equal(HttpResultErrorCode.Forbidden, result.ErrorCode);
  }

    private static PrincipalDescriptor Actor(Guid principalId, Guid tenantId) =>
      new(PrincipalType.User, principalId, tenantId, "test");

  private static async Task SeedAssignment(TestApp testApp, PermissionAssignment assignment)
  {
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static async Task SeedLogonToken(TestApp testApp, Guid tokenId, Guid userId, Guid deviceId, Guid tenantId)
  {
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.LogonTokens.Add(new LogonToken
    {
      Id = tokenId,
      Token = $"test-logon-token-{tokenId:N}",
      UserId = userId,
      DeviceId = deviceId,
      TenantId = tenantId,
      ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
    });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static async Task SeedPersonalAccessToken(TestApp testApp, Guid tokenId, Guid userId)
  {
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PersonalAccessTokens.Add(new PersonalAccessToken
    {
      Id = tokenId,
      Name = $"pat-{tokenId:N}",
      HashedKey = "test-hashed-key",
      UserId = userId,
      PermissionMode = PersonalAccessTokenPermissionMode.Restricted
    });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static async Task SeedServerServiceAccountWithId(TestApp testApp, Guid accountId)
  {
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Id = accountId,
      Kind = ServiceAccountKind.Server,
      Name = $"server-sa-{accountId:N}",
      IsEnabled = true,
      AccessMode = ServiceAccountAccessMode.Restricted
    });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static async Task SeedUserWithId(TestApp testApp, Guid userId, Guid tenantId)
  {
    using var scope = testApp.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var result = await userManager.CreateAsync(new AppUser
    {
      Id = userId,
      UserName = $"colliding-{userId:N}@t.local",
      NormalizedUserName = $"COLLIDING-{userId:N}@T.LOCAL".ToUpperInvariant(),
      Email = $"colliding-{userId:N}@t.local",
      NormalizedEmail = $"COLLIDING-{userId:N}@T.LOCAL".ToUpperInvariant(),
      EmailConfirmed = true,
      TenantId = tenantId
    });
    Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(x => x.Description)));
  }

  private static InternalDtos.CreatePermissionAssignmentRequestDto ServerScopeDeviceReadRequest(Guid principalId) =>
    new(
      PermissionPrincipalKind.User,
      principalId,
      PermissionNames.DeviceRead,
      PermissionEffect.Allow,
      PermissionScopeKind.Server,
      null,
      null);
}
