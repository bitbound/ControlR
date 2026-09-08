using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.PermissionAssignments;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// PAT scope-creation flow: optional least-privilege scopes are validated against the
/// owner's effective permissions and persisted as PAT-principal assignment rows.
/// </summary>
public class PersonalAccessTokenScopeTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateAssignment_ForPatPrincipal_OutsideOwnerPermissions_Fails()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id,
      $"pat-owner-{Guid.NewGuid():N}@t.local",
      PermissionPresets.TenantAdministrator);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var patManager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();
    var assignmentManager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Panel-Scoped PAT", PersonalAccessTokenPermissionMode.InheritOwner), user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess);

    var tokenId = patResult.Value.PersonalAccessToken.Id;

    var result = await assignmentManager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.PersonalAccessToken,
        tokenId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Device,
        device.Id,
        null),
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test"),
      TestContext.Current.CancellationToken);

    Assert.False(result.IsSuccess);
    Assert.Equal(HttpResultErrorCode.BadRequest, result.ErrorCode);
  }

  [Fact]
  public async Task CreateAssignment_ForPatPrincipal_WithinOwnerPermissions_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id,
      $"pat-owner-{Guid.NewGuid():N}@t.local",
      PermissionPresets.DeviceSuperUser,
      PermissionPresets.TenantAdministrator);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var patManager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();
    var assignmentManager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Panel-Scoped PAT", PersonalAccessTokenPermissionMode.InheritOwner), user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess);

    var tokenId = patResult.Value.PersonalAccessToken.Id;

    var result = await assignmentManager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.PersonalAccessToken,
        tokenId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Device,
        device.Id,
        null),
      tenant.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, tenant.Id, "test"),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, $"Expected panel assignment to succeed: {result.Reason}");
  }

  [Fact]
  public async Task CreateTokenWithKey_InvalidPermissionMode_Fails()
  {
    // Bootstrap path's Enum.IsDefined guard should reject an invalid permission mode.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var invalidMode = (PersonalAccessTokenPermissionMode)999;
    var result = await manager.CreateTokenWithKey(
      Guid.NewGuid(), "x".PadLeft(32, 'a'), "Test Token", user.Id, invalidMode);

    Assert.False(result.IsSuccess);
    Assert.Contains("PermissionMode is not a valid value", result.Reason);
  }

  [Fact]
  public async Task CreateTokenWithKey_RejectsExistingId()
  {
    // Bootstrap path must not silently overwrite an existing token (and its scopes) when the
    // caller supplies a token ID that is already in use.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local");

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var tokenId = Guid.NewGuid();
    var secret = "x".PadLeft(32, 'a');

    var first = await manager.CreateTokenWithKey(
      tokenId, secret, "First Token", user.Id, PersonalAccessTokenPermissionMode.Restricted);
    Assert.True(first.IsSuccess, $"First creation failed: {first.Reason}");

    var second = await manager.CreateTokenWithKey(
      tokenId, secret, "Second Token", user.Id, PersonalAccessTokenPermissionMode.Restricted);
    Assert.False(second.IsSuccess);
    Assert.Contains("already exists", second.Reason);
  }

  [Fact]
  public async Task CreateToken_InheritOwnerByCredentialScopedActor_FailsAndCreatesNothing()
  {
    // Credential laundering guard: an InheritOwner token evaluates as the owner's full
    // effective permissions, so a credential-scoped actor (PAT or logon token) must not be
    // able to mint one. Only full-identity sessions may.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var patActor = new PrincipalDescriptor(
      PrincipalType.User,
      user.Id,
      user.TenantId,
      "test",
      CredentialId: Guid.NewGuid(),
      CredentialType: CredentialType.PersonalAccessToken);

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Laundered PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      user.Id,
      patActor);

    Assert.False(result.IsSuccess);

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tokenCount = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .CountAsync(x => x.UserId == user.Id, TestContext.Current.CancellationToken);
    Assert.Equal(0, tokenCount);
  }

  [Fact]
  public async Task CreateToken_InheritOwnerWithScopes_FailsAndCreatesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Inherit-With-Scopes PAT",
        PersonalAccessTokenPermissionMode.InheritOwner,
        [new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id)]),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    Assert.False(result.IsSuccess);

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tokenCount = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .CountAsync(x => x.UserId == user.Id, TestContext.Current.CancellationToken);
    Assert.Equal(0, tokenCount);
  }

  [Fact]
  public async Task CreateToken_RestrictedByCredentialScopedActor_Succeeds()
  {
    // A scoped credential may still mint Restricted tokens. Their scopes are validated
    // against the owner, so no privilege beyond the owner's grants is possible.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var patActor = new PrincipalDescriptor(
      PrincipalType.User,
      user.Id,
      user.TenantId,
      "test",
      CredentialId: Guid.NewGuid(),
      CredentialType: CredentialType.PersonalAccessToken);

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Scoped-from-scoped PAT",
        PersonalAccessTokenPermissionMode.Restricted,
        Scopes: [new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id)]),
      user.Id,
      patActor);

    Assert.True(result.IsSuccess, $"Expected Restricted PAT creation to succeed: {result.Reason}");
  }

  [Fact]
  public async Task CreateToken_RestrictedWithoutScopes_SucceedsAndWritesNoScopeRows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local");

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Empty Restricted PAT", PersonalAccessTokenPermissionMode.Restricted),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    Assert.True(result.IsSuccess);

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tokenId = result.Value.PersonalAccessToken.Id;

    var scopeRows = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .CountAsync(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                       x.PrincipalId == tokenId,
        TestContext.Current.CancellationToken);
    Assert.Equal(0, scopeRows);

    var token = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .FirstAsync(x => x.Id == tokenId, TestContext.Current.CancellationToken);
    Assert.Equal(PersonalAccessTokenPermissionMode.Restricted, token.PermissionMode);
  }

  [Fact]
  public async Task CreateToken_WithoutScopes_RemainsUserEquivalent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local");

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Unscoped PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    Assert.True(result.IsSuccess);

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var scopeRows = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .CountAsync(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                       x.PrincipalId == result.Value.PersonalAccessToken.Id,
        TestContext.Current.CancellationToken);
    Assert.Equal(0, scopeRows);

    var token = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .FirstAsync(x => x.Id == result.Value.PersonalAccessToken.Id, TestContext.Current.CancellationToken);
    Assert.Equal(PersonalAccessTokenPermissionMode.InheritOwner, token.PermissionMode);
  }

  [Fact]
  public async Task CreateToken_WithScopeOutsideOwnerPermissions_FailsAndCreatesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local");
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Scoped PAT", PersonalAccessTokenPermissionMode.Restricted,
        Scopes: [new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id)]),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    Assert.False(result.IsSuccess);

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tokenCount = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .CountAsync(x => x.UserId == user.Id, TestContext.Current.CancellationToken);
    Assert.Equal(0, tokenCount);
  }

  [Fact]
  public async Task CreateToken_WithValidScopes_OnPostgres_WritesScopeRowsKeyedByRealTokenId()
  {
    // Regression test: entity Ids are database-generated (gen_random_uuid()) on Postgres, so
    // scope rows must be written after the token is saved. A pre-save Id would be Guid.Empty,
    // orphaning the scope rows and silently granting the PAT the owner's full permissions.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, useInMemoryDatabase: false);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Scoped PAT", PersonalAccessTokenPermissionMode.Restricted,
        Scopes: [new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id)]),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    Assert.True(result.IsSuccess, $"Scoped PAT creation failed: {result.Reason}");

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tokenId = result.Value.PersonalAccessToken.Id;

    Assert.NotEqual(Guid.Empty, tokenId);

    var scopeRows = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken && x.PrincipalId == tokenId)
      .ToListAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(scopeRows);
    Assert.Equal(PermissionNames.DeviceRead, row.PermissionName);
    Assert.Equal(PermissionScopeKind.Device, row.ScopeKind);
    Assert.Equal(device.Id, row.ScopeId);
    Assert.Equal(tenant.Id, row.OwningTenantId);

    var changeLogCount = await db.AuthorizationChangeLogs
      .IgnoreQueryFilters()
      .CountAsync(x => x.TargetId == tokenId, TestContext.Current.CancellationToken);
    Assert.Equal(1, changeLogCount);
  }

  [Fact]
  public async Task CreateToken_WithValidScopes_WritesScopeRows()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var result = await manager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Scoped PAT", PersonalAccessTokenPermissionMode.Restricted,
        Scopes: [new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id)]),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    Assert.True(result.IsSuccess, $"Scoped PAT creation failed: {result.Reason}");

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var tokenId = result.Value.PersonalAccessToken.Id;

    var scopeRows = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken && x.PrincipalId == tokenId)
      .ToListAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(scopeRows);
    Assert.Equal(PermissionNames.DeviceRead, row.PermissionName);
    Assert.Equal(PermissionScopeKind.Device, row.ScopeKind);
    Assert.Equal(device.Id, row.ScopeId);
    Assert.Equal(tenant.Id, row.OwningTenantId);

    var changeLogCount = await db.AuthorizationChangeLogs
      .IgnoreQueryFilters()
      .CountAsync(x => x.TargetId == tokenId, TestContext.Current.CancellationToken);
    Assert.Equal(1, changeLogCount);
  }

  [Fact]
  public async Task Delete_RemovesAssignmentRows()
  {
    // Regression: PermissionAssignment is a polymorphic principal with no FK cascade, so
    // deleting a PAT must also remove its assignment rows or a new PAT reusing the same ID
    // would inherit the deleted token's scopes.
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.App.Services.CreateTestTenant();
    await testApp.App.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testApp.App.Services.CreateTestUser(
      tenant.Id, $"pat-owner-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);
    var device = await testApp.App.Services.CreateTestDevice(tenant.Id);

    using var scope = testApp.CreateScope();
    var patManager = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokenManager>();

    var createResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Scoped PAT", PersonalAccessTokenPermissionMode.Restricted,
        Scopes: [new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Device, device.Id)]),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(createResult.IsSuccess, $"Scoped PAT creation failed: {createResult.Reason}");
    var tokenId = createResult.Value.PersonalAccessToken.Id;

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var beforeCount = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .CountAsync(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken && x.PrincipalId == tokenId,
        TestContext.Current.CancellationToken);
    Assert.Equal(1, beforeCount);

    var deleteResult = await patManager.Delete(tokenId, user.Id);
    Assert.True(deleteResult.IsSuccess, $"Delete failed: {deleteResult.Reason}");

    var afterCount = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .CountAsync(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken && x.PrincipalId == tokenId,
        TestContext.Current.CancellationToken);
    Assert.Equal(0, afterCount);

    var tokenExists = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .AnyAsync(x => x.Id == tokenId, TestContext.Current.CancellationToken);
    Assert.False(tokenExists);
  }
}
