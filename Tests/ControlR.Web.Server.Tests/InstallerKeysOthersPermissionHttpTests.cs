using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Installer-key gating through the full HTTP authorization pipeline, which is where the
/// self/others permission model is actually enforced. A caller holding only an
/// installer-key.others.* permission must reach the shared list, usages, delete, and rename
/// endpoints, because the others pair is a stand-alone auditor/admin grant covering every key
/// in the tenant, including the caller's own. Key creation stays self-scoped: no others
/// permission may create a key.
/// </summary>
public class InstallerKeysOthersPermissionHttpTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task InternalCreate_WhenCallerHasOthersWriteOnly_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, keyManager, _, _) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, keyManager.Id,
      PermissionNames.InstallerKeyOthersWrite);

    using var httpClient = await CreatePatClient(testServer, keyManager);
    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.Internal.InstallerKeysEndpoint,
      new InternalDtos.CreateInstallerKeyRequestDto(InstallerKeyType.Persistent),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task InternalDelete_WhenCallerHasOthersWriteOnly_RemovesAnotherUsersKey()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, keyManager, keyOfA, keyOfB) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, keyManager.Id,
      PermissionNames.InstallerKeyOthersWrite);

    using var httpClient = await CreatePatClient(testServer, keyManager);
    var response = await httpClient.DeleteAsync(
      $"{HttpConstants.Internal.InstallerKeysEndpoint}/{keyOfA}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var remaining = await ListKeysViaManagerAsync(testServer, tenant.Id, keyManager.Id);
    Assert.DoesNotContain(remaining, x => x.Id == keyOfA);
    Assert.Contains(remaining, x => x.Id == keyOfB);
  }

  [Fact]
  public async Task InternalGetAll_WhenCallerHasOthersReadOnly_ReturnsAllTenantKeys()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, auditor, keyOfA, keyOfB) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, auditor.Id,
      PermissionNames.InstallerKeyOthersRead);

    using var httpClient = await CreatePatClient(testServer, auditor);
    var response = await httpClient.GetAsync(
      HttpConstants.Internal.InstallerKeysEndpoint,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var items = await response.Content.ReadFromJsonAsync<List<InternalDtos.AgentInstallerKeyDto>>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(items);
    Assert.Contains(items, x => x.Id == keyOfA);
    Assert.Contains(items, x => x.Id == keyOfB);
  }

  [Fact]
  public async Task V1Create_WhenCallerHasOthersWriteOnly_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, auditor, _, _) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, auditor.Id,
      PermissionNames.InstallerKeyOthersWrite);

    using var httpClient = await CreatePatClient(testServer, auditor);
    var response = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.InstallerKeysEndpoint,
      new CreateInstallerKeyRequestDto(tenant.Id, InstallerKeyType.Persistent),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task V1Delete_WhenCallerHasOthersWriteOnly_RemovesAnotherUsersKey()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, keyManager, keyOfA, keyOfB) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, keyManager.Id,
      PermissionNames.InstallerKeyOthersWrite);

    using var httpClient = await CreatePatClient(testServer, keyManager);
    var response = await httpClient.DeleteAsync(
      $"{HttpConstants.V1.InstallerKeysEndpoint}/{keyOfA}?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var remaining = await ListKeysViaManagerAsync(testServer, tenant.Id, keyManager.Id);
    Assert.DoesNotContain(remaining, x => x.Id == keyOfA);
    Assert.Contains(remaining, x => x.Id == keyOfB);
  }

  [Fact]
  public async Task V1GetAll_WhenCallerHasOthersReadOnly_ReturnsAllTenantKeys()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, auditor, keyOfA, keyOfB) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, auditor.Id,
      PermissionNames.InstallerKeyOthersRead);

    using var httpClient = await CreatePatClient(testServer, auditor);
    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.InstallerKeysEndpoint}?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<InstallerKeysResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Contains(body.Items, x => x.Id == keyOfA);
    Assert.Contains(body.Items, x => x.Id == keyOfB);
  }

  [Fact]
  public async Task V1GetAll_WhenCallerHasSelfReadOnly_ReturnsOnlyOwnKeys()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, ownerA, _, keyOfA, keyOfB) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, ownerA.Id,
      PermissionNames.InstallerKeySelfRead);

    using var httpClient = await CreatePatClient(testServer, ownerA);
    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.InstallerKeysEndpoint}?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<InstallerKeysResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Contains(body.Items, x => x.Id == keyOfA);
    Assert.DoesNotContain(body.Items, x => x.Id == keyOfB);
  }

  [Fact]
  public async Task V1GetUsages_WhenCallerHasOthersReadOnly_ReturnsOk()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, auditor, keyOfA, _) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, auditor.Id,
      PermissionNames.InstallerKeyOthersRead);

    using var httpClient = await CreatePatClient(testServer, auditor);
    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.InstallerKeysEndpoint}/{keyOfA}/usages?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task V1Rename_WhenCallerHasOthersWriteOnly_ReturnsNoContent()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenant, _, keyManager, keyOfA, _) = await SeedTenantWithKeysAsync(testServer);
    await GrantTenantPermissions(testServer.Services, tenant.Id, keyManager.Id,
      PermissionNames.InstallerKeyOthersWrite);

    using var httpClient = await CreatePatClient(testServer, keyManager);
    var response = await httpClient.PutAsJsonAsync(
      $"{HttpConstants.V1.InstallerKeysEndpoint}/{keyOfA}?tenantId={tenant.Id}",
      new RenameInstallerKeyRequestDto("renamed by others writer"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  private static async Task<HttpClient> CreatePatClient(TestWebServer testServer, AppUser user)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var actor = new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test");
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "Installer Key Others Test PAT",
        PersonalAccessTokenPermissionMode.InheritOwner),
      actor.PrincipalId,
      actor);
    Assert.True(patResult.IsSuccess, $"PAT creation failed: {patResult.Reason}");

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }

  private static async Task GrantTenantPermissions(
    IServiceProvider services,
    Guid tenantId,
    Guid userId,
    params string[] permissionNames)
  {
    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    foreach (var permissionName in permissionNames)
    {
      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        userId,
        permissionName,
        PermissionScopeKind.Tenant,
        tenantId,
        tenantId,
        createdBy: null));
    }

    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private static async Task<List<InternalDtos.AgentInstallerKeyDto>> ListKeysViaManagerAsync(
    TestWebServer testServer,
    Guid tenantId,
    Guid callerId)
  {
    using var scope = testServer.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IAgentInstallerKeyManager>();
    return [.. await manager.GetAllKeys(tenantId, callerId, isTenantAdmin: true)];
  }

  private async Task<SeedResult> SeedTenantWithKeysAsync(TestWebServer testServer)
  {
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var ownerA = await testServer.Services.CreateTestUser(
      tenant.Id, $"owner-a-{Guid.NewGuid():N}@t.local");
    var ownerB = await testServer.Services.CreateTestUser(
      tenant.Id, $"owner-b-{Guid.NewGuid():N}@t.local");

    var manager = testServer.Services.GetRequiredService<IAgentInstallerKeyManager>();
    var keyOfA = await manager.CreateKey(
      tenant.Id, ownerA.Id, InstallerKeyCreatorKind.User, InstallerKeyType.Persistent,
      allowedUses: null, expiration: null, friendlyName: "key of A");
    var keyOfB = await manager.CreateKey(
      tenant.Id, ownerB.Id, InstallerKeyCreatorKind.User, InstallerKeyType.Persistent,
      allowedUses: null, expiration: null, friendlyName: "key of B");

    return new SeedResult(tenant, ownerA, ownerB, keyOfA.Id, keyOfB.Id);
  }

  private sealed record SeedResult(
    Tenant Tenant,
    AppUser OwnerA,
    AppUser OwnerB,
    Guid KeyOfA,
    Guid KeyOfB);
}
