using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.PermissionAssignments;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Write-gate matrix: one cell per (principal case × effect × scope kind × permission family)
/// equivalence class, asserting accept-or-reject of <c>Create</c> under a fully-authorized
/// (god-mode) actor, so principal-legality rules are pinned per-cell rather than per-anecdote.
/// Actor-authority cells (who may write at all) live in the gate-specific tests; this matrix
/// pins what may exist.
/// </summary>
public class PermissionWriteMatrixTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  public static TheoryData<PrincipalCase, string, PermissionEffect, PermissionScopeKind, bool> Cells => new()
  {
    // Tenant-addressable allow at Server boundary: only server service accounts (I1).
    { PrincipalCase.User, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Server, false },
    { PrincipalCase.UserGroup, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Server, false },
    { PrincipalCase.TenantServiceAccount, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Server, false },
    { PrincipalCase.PersonalAccessToken, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Server, false },
    { PrincipalCase.LogonTokenMatchingDevice, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Server, false },
    { PrincipalCase.ServerServiceAccount, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Server, true },
    // Denies are exempt from the reach rule at every tenant-bound principal (I2)...
    { PrincipalCase.User, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Server, true },
    { PrincipalCase.TenantServiceAccount, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Server, true },
    { PrincipalCase.ServerServiceAccount, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Server, true },
    { PrincipalCase.PersonalAccessToken, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Server, true },
    // ...but logon tokens carry only Device-to-own-device rows; everything else is inert (I4).
    { PrincipalCase.LogonTokenMatchingDevice, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Server, false },
    { PrincipalCase.LogonTokenMatchingDevice, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Tenant, false },
    { PrincipalCase.LogonTokenMatchingDevice, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Tenant, false },
    { PrincipalCase.LogonTokenMatchingDevice, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Device, true },
    { PrincipalCase.LogonTokenMatchingDevice, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Device, true },
    { PrincipalCase.LogonTokenOtherDevice, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Device, false },
    { PrincipalCase.LogonTokenOtherDevice, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Device, false },
    // Tenant-bound scopes stay legal for every principal (the baseline rows).
    { PrincipalCase.User, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Tenant, true },
    { PrincipalCase.User, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Tenant, true },
    { PrincipalCase.UserGroup, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Tenant, true },
    { PrincipalCase.TenantServiceAccount, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Tenant, true },
    { PrincipalCase.ServerServiceAccount, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Tenant, true },
    { PrincipalCase.PersonalAccessToken, PermissionNames.DeviceRead, PermissionEffect.Allow, PermissionScopeKind.Tenant, true },
    { PrincipalCase.PersonalAccessToken, PermissionNames.DeviceRead, PermissionEffect.Deny, PermissionScopeKind.Tenant, true },
    // Control-plane carve-out: server-only permissions are Server-scoped by nature and users
    // must be able to hold them (allow and deny).
    { PrincipalCase.User, PermissionNames.ServerAlertsRead, PermissionEffect.Allow, PermissionScopeKind.Server, true },
    { PrincipalCase.User, PermissionNames.ServerAlertsRead, PermissionEffect.Deny, PermissionScopeKind.Server, true },
    { PrincipalCase.ServerServiceAccount, PermissionNames.ServerAlertsRead, PermissionEffect.Allow, PermissionScopeKind.Server, true },
    // Whitelist rejections that predate I1 stay rejected at Server for the right reasons.
    { PrincipalCase.User, PermissionNames.TenantRead, PermissionEffect.Allow, PermissionScopeKind.Server, false },
    { PrincipalCase.User, PermissionNames.UserGroupAssignUsers, PermissionEffect.Allow, PermissionScopeKind.Server, false }
  };

  [Theory]
  [MemberData(nameof(Cells))]
  public async Task Create_CellOutcome_MatchesInvariant(
    PrincipalCase principalCase,
    string permissionName,
    PermissionEffect effect,
    PermissionScopeKind scopeKind,
    bool expectSuccess)
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput,
      testDatabaseName: $"{Guid.NewGuid()}");

    var tenant = await testApp.Services.CreateTestTenant();
    await testApp.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var actor = await testApp.Services.CreateTestUser(tenant.Id, email: $"actor-{Guid.NewGuid():N}@t.local");
    await SeedGodActorAsync(testApp, actor.Id, tenant.Id);

    var tokenDevice = await testApp.Services.CreateTestDevice(tenant.Id);
    var otherDevice = await testApp.Services.CreateTestDevice(tenant.Id);

    PermissionPrincipalKind kind;
    Guid principalId;
    Guid? scopeId;

    switch (principalCase)
    {
      case PrincipalCase.User:
        kind = PermissionPrincipalKind.User;
        principalId = (await testApp.Services.CreateTestUser(tenant.Id, email: $"target-{Guid.NewGuid():N}@t.local")).Id;
        scopeId = null;
        break;
      case PrincipalCase.UserGroup:
        kind = PermissionPrincipalKind.UserGroup;
        principalId = await SeedUserGroupAsync(testApp, tenant.Id);
        scopeId = null;
        break;
      case PrincipalCase.TenantServiceAccount:
        kind = PermissionPrincipalKind.ServiceAccount;
        principalId = await SeedServiceAccountAsync(testApp, tenant.Id, ServiceAccountKind.Tenant);
        scopeId = null;
        break;
      case PrincipalCase.ServerServiceAccount:
        kind = PermissionPrincipalKind.ServiceAccount;
        principalId = await SeedServiceAccountAsync(testApp, tenant.Id, ServiceAccountKind.Server);
        scopeId = null;
        break;
      case PrincipalCase.PersonalAccessToken:
        kind = PermissionPrincipalKind.PersonalAccessToken;
        var owner = await testApp.Services.CreateTestUser(tenant.Id, email: $"owner-{Guid.NewGuid():N}@t.local");
        await SeedOwnerDeviceReadAsync(testApp, owner.Id, tenant.Id);
        principalId = Guid.NewGuid();
        await SeedPersonalAccessTokenAsync(testApp, principalId, owner.Id);
        scopeId = null;
        break;
      case PrincipalCase.LogonTokenMatchingDevice:
      case PrincipalCase.LogonTokenOtherDevice:
        kind = PermissionPrincipalKind.LogonToken;
        var recipient = await testApp.Services.CreateTestUser(tenant.Id, email: $"recipient-{Guid.NewGuid():N}@t.local");
        await SeedOwnerDeviceReadAsync(testApp, recipient.Id, tenant.Id);
        principalId = Guid.NewGuid();
        await SeedLogonTokenAsync(testApp, principalId, recipient.Id, tokenDevice.Id, tenant.Id);
        scopeId = scopeKind == PermissionScopeKind.Device
          ? principalCase == PrincipalCase.LogonTokenMatchingDevice ? tokenDevice.Id : otherDevice.Id
          : null;
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(principalCase));
    }

    // Tenant-scope rows normalize scopeId to the acting tenant; Device rows carry their device.
    if (scopeKind == PermissionScopeKind.Tenant)
    {
      scopeId = tenant.Id;
    }

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var result = await manager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        kind, principalId, permissionName, effect, scopeKind, scopeId, null),
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, actor.Id, tenant.Id, "test"),
      TestContext.Current.CancellationToken);

    Assert.True(
      result.IsSuccess == expectSuccess,
      $"{principalCase} {effect} {permissionName}@{scopeKind}: expected {(expectSuccess ? "success" : "rejection")}, got Success={result.IsSuccess} Code={result.ErrorCode} Reason={result.Reason}");
  }

  private static async Task SeedAssignmentAsync(TestApp testApp, PermissionAssignment assignment)
  {
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(assignment);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static async Task SeedGodActorAsync(TestApp testApp, Guid actorId, Guid tenantId)
  {
    var self = new PrincipalDescriptor(PrincipalType.User, actorId, tenantId, "test");
    await SeedAssignmentAsync(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User, actorId, PermissionNames.ServerPermissionsWrite,
      PermissionScopeKind.Server, null, tenantId, self));
    await SeedAssignmentAsync(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User, actorId, PermissionNames.TenantPermissionsWrite,
      PermissionScopeKind.Tenant, tenantId, tenantId, self));
    await SeedAssignmentAsync(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User, actorId, PermissionNames.TenantPermissionsDeny,
      PermissionScopeKind.Tenant, tenantId, tenantId, self));
  }

  private static async Task SeedLogonTokenAsync(TestApp testApp, Guid tokenId, Guid userId, Guid deviceId, Guid tenantId)
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

  private static async Task SeedOwnerDeviceReadAsync(TestApp testApp, Guid userId, Guid tenantId)
  {
    await SeedAssignmentAsync(testApp, PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User, userId, PermissionNames.DeviceRead,
      PermissionScopeKind.Tenant, tenantId, tenantId,
      new PrincipalDescriptor(PrincipalType.User, userId, tenantId, "test")));
  }

  private static async Task SeedPersonalAccessTokenAsync(TestApp testApp, Guid tokenId, Guid userId)
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

  private static async Task<Guid> SeedServiceAccountAsync(TestApp testApp, Guid tenantId, ServiceAccountKind kind)
  {
    var id = Guid.NewGuid();
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.ServiceAccounts.Add(new ServiceAccount
    {
      Id = id,
      Kind = kind,
      TenantId = kind == ServiceAccountKind.Server ? null : tenantId,
      Name = $"{kind.ToString().ToLowerInvariant()}-{id:N}",
      IsEnabled = true,
      AccessMode = ServiceAccountAccessMode.Restricted
    });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    return id;
  }

  private static async Task<Guid> SeedUserGroupAsync(TestApp testApp, Guid tenantId)
  {
    var id = Guid.NewGuid();
    using var scope = testApp.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.UserGroups.Add(new UserGroup { Id = id, Name = $"group-{id:N}", TenantId = tenantId });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    return id;
  }

  public enum PrincipalCase
  {
    User,
    UserGroup,
    TenantServiceAccount,
    ServerServiceAccount,
    PersonalAccessToken,
    LogonTokenMatchingDevice,
    LogonTokenOtherDevice
  }
}
