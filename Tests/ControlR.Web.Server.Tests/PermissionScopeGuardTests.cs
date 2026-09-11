using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using PADtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Server.Tests;

public class PermissionScopeGuardTests(ITestOutputHelper testOutput)
{
  [Theory]
  [InlineData(PermissionNames.ServerTenantsWrite, PermissionScopeKind.Server)]
  [InlineData(PermissionNames.TenantPermissionsWrite, PermissionScopeKind.Tenant)]
  [InlineData(PermissionNames.DeviceRead, PermissionScopeKind.Server)]
  [InlineData(PermissionNames.UserGroupAssignUsers, PermissionScopeKind.Tenant)]
  [InlineData(PermissionNames.DeviceGroupAssignDevices, PermissionScopeKind.Tenant)]
  [InlineData(PermissionNames.DeviceLogonTokenCreate, PermissionScopeKind.Server)]
  public void Catalog_GetBroadestLegalScope_ResolvesToExpectedScope(string permissionName, PermissionScopeKind expected)
  {
    Assert.Equal(expected, PermissionCatalog.GetBroadestLegalScope(permissionName));
  }

  [Fact]
  public async Task Create_DeviceReadAtTenantScope_ReturnsCreated()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var response = await client.PostAsJsonAsync(
      PaUrl(tenantId),
      new PADtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task Create_ServerOnlyPermissionAtTenantScope_ReturnsBadRequest()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var response = await client.PostAsJsonAsync(
      PaUrl(tenantId),
      new PADtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userId,
        PermissionNames.ServerTenantsWrite,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Delete_AnotherPrincipalsProtectedPermission_ReturnsOk()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var otherAdmin = await testServer.Services.CreateTestUser(
      tenantId, $"admin-{Guid.NewGuid():N}@t.local", PermissionPresets.TenantAdministrator);

    var otherAssignment = await GetAssignment(client, tenantId, otherAdmin.Id, PermissionNames.TenantPermissionsWrite);

    var response = await client.DeleteAsync(
      PaUrl(tenantId, $"/{otherAssignment.Id}"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task Delete_AnotherUser_ReturnsNoContent()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var otherUser = await testServer.Services.CreateTestUser(
      tenantId, $"user-{Guid.NewGuid():N}@t.local");

    var response = await client.DeleteAsync(
      $"{HttpConstants.Internal.UsersEndpoint}/{otherUser.Id}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task Delete_OwnLastProtectedPermission_ReturnsBadRequest()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var assignment = await GetAssignment(client, tenantId, userId, PermissionNames.TenantPermissionsWrite);

    var response = await client.DeleteAsync(
      PaUrl(tenantId, $"/{assignment.Id}"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var stillHeld = await GetAssignment(client, tenantId, userId, PermissionNames.TenantPermissionsWrite);
    Assert.NotNull(stillHeld);
  }

  [Fact]
  public async Task Delete_OwnUser_ReturnsBadRequest()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var response = await client.DeleteAsync(
      $"{HttpConstants.Internal.UsersEndpoint}/{userId}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public void DevicePermissions_AllowServerScope()
  {
    var devicePermissions = PermissionCatalog.All.Values
      .Where(metadata => metadata.AllowedScopeKinds.Contains(PermissionScopeKind.Device))
      .ToArray();

    Assert.NotEmpty(devicePermissions);
    Assert.All(devicePermissions, metadata =>
      Assert.Contains(PermissionScopeKind.Server, metadata.AllowedScopeKinds));
  }

  [Fact]
  public void DeviceResourcePolicies_AllPermissionsExistInCatalog()
  {
    foreach (var (policyName, permissionName) in DeviceResourcePolicies.PolicyToPermission)
    {
      Assert.True(
        PermissionCatalog.Exists(permissionName),
        $"Device resource policy '{policyName}' references permission '{permissionName}', which does not exist in the permission catalog.");
    }
  }

  [Fact]
  public async Task Disable_OwnLastProtectedPermission_ReturnsBadRequest()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var assignment = await GetAssignment(client, tenantId, userId, PermissionNames.TenantPermissionsWrite);

    var response = await client.PutAsJsonAsync(
      PaUrl(tenantId, $"/{assignment.Id}"),
      new PADtos.UpdatePermissionAssignmentRequestDto(
        assignment.PermissionName,
        assignment.Effect,
        assignment.ScopeKind,
        assignment.ScopeId,
        assignment.Notes,
        IsEnabled: false),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Edit_OwnLastProtectedPermissionAway_ReturnsBadRequest()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var assignment = await GetAssignment(client, tenantId, userId, PermissionNames.TenantPermissionsWrite);

    var response = await client.PutAsJsonAsync(
      PaUrl(tenantId, $"/{assignment.Id}"),
      new PADtos.UpdatePermissionAssignmentRequestDto(
        PermissionNames.TenantRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null,
        IsEnabled: true),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public void PermissionCatalog_AllPermissionsHaveLegalPresetScope()
  {
    foreach (var (permissionName, metadata) in PermissionCatalog.All)
    {
      var broadest = PermissionCatalog.GetBroadestTenantLegalScope(permissionName) ??
                     PermissionCatalog.GetBroadestLegalScope(permissionName);
      Assert.NotNull(broadest);
      Assert.True(
        broadest is PermissionScopeKind.Server or PermissionScopeKind.Tenant,
        $"Permission '{permissionName}' has broadest seedable scope '{broadest}', which cannot be emitted by a preset.");
      Assert.Contains(broadest.Value, metadata.AllowedScopeKinds);
    }
  }

  [Fact]
  public void PermissionMetadata_AllCatalogEntriesHaveExplicitScopeKinds()
  {
    Assert.All(PermissionCatalog.All, entry =>
    {
      Assert.False(entry.Key is null or "");
      Assert.Equal(entry.Key, entry.Value.Name);
      Assert.NotEmpty(entry.Value.AllowedScopeKinds);
      Assert.Equal(
        entry.Value.AllowedScopeKinds.Length,
        entry.Value.AllowedScopeKinds.Distinct().Count());
      Assert.All(entry.Value.AllowedScopeKinds, scopeKind =>
        Assert.True(Enum.IsDefined(scopeKind)));
    });
  }

  [Fact]
  public void PermissionMetadata_NonTenantAddressablePermissionsAreServerOnly()
  {
    // The Allow at Server scope is restricted to server service accounts only for
    // tenant-addressable permissions (see ValidatePermissionScope). That exemption for
    // non-tenant-addressable permissions is only safe because such permissions are server
    // administration whose sole allowed scope is already Server. Guard that invariant, which
    // the PermissionMetadata.AllowsTenantScope doc comment relies on.
    foreach (var (permissionName, metadata) in PermissionCatalog.All)
    {
      if (metadata.AllowsTenantScope)
      {
        continue;
      }

      Assert.Equal([PermissionScopeKind.Server], metadata.AllowedScopeKinds);
    }
  }

  [Fact]
  public void PermissionScopeKinds_GetBroadestTenantLegalScope_ExcludesServer()
  {
    // Device permissions (Option A) now include Server in their allowed scope kinds. The
    // tenant-bound variant must resolve to the broadest non-Server kind so tenant-facing UI
    // defaults and preset seeding never pre-select a cross-tenant server grant.
    var deviceKinds = new[]
    {
      PermissionScopeKind.Device,
      PermissionScopeKind.DeviceGroup,
      PermissionScopeKind.CustomerTenant,
      PermissionScopeKind.Tenant,
      PermissionScopeKind.Server
    };
    Assert.Equal(PermissionScopeKind.Tenant, PermissionScopeKinds.GetBroadestTenantLegalScope(deviceKinds));
    Assert.Equal(PermissionScopeKind.Server, PermissionScopeKinds.GetBroadestLegalScope(deviceKinds));

    // Server-only permissions fall back to the overall broadest legal scope.
    var serverOnly = new[] { PermissionScopeKind.Server };
    Assert.Equal(PermissionScopeKind.Server, PermissionScopeKinds.GetBroadestTenantLegalScope(serverOnly));

    // Tenant-only permissions are unchanged.
    var tenantOnly = new[] { PermissionScopeKind.Tenant };
    Assert.Equal(PermissionScopeKind.Tenant, PermissionScopeKinds.GetBroadestTenantLegalScope(tenantOnly));

    // Empty set resolves to null.
    Assert.Null(PermissionScopeKinds.GetBroadestTenantLegalScope(Array.Empty<PermissionScopeKind>()));
  }

  [Fact]
  public void PermissionScopeKind_AllValuesAreHandledByScopeBreadth()
  {
    var expectedKinds = Enum.GetValues<PermissionScopeKind>()
      .Except([PermissionScopeKind.Unknown, PermissionScopeKind.Server, PermissionScopeKind.Tenant,
        PermissionScopeKind.Device, PermissionScopeKind.DeviceGroup,
        PermissionScopeKind.CustomerTenant, PermissionScopeKind.UserGroup])
      .ToArray();

    Assert.Empty(expectedKinds);

    foreach (var scopeKind in Enum.GetValues<PermissionScopeKind>())
    {
      if (scopeKind == PermissionScopeKind.Unknown)
      {
        continue;
      }

      var result = PermissionScopeKinds.GetBroadestLegalScope([scopeKind]);
      Assert.Equal(scopeKind, result);
    }
  }

  [Fact]
  public void PresetSeedableScope_NeverSeedsDevicePermissionsAtServerScope()
  {
    // Device permissions must seed (via presets) at tenant scope, never at server scope,
    // so preset grants never grant cross-tenant device access.
    foreach (var permission in PermissionPresets.GetPermissions(PermissionPresets.DeviceSuperUser))
    {
      if (PermissionCatalog.Get(permission)?.AllowedScopeKinds.Contains(PermissionScopeKind.Device) != true)
      {
        continue;
      }

      var seedScope = PermissionCatalog.GetBroadestTenantLegalScope(permission) ?? PermissionScopeKind.Tenant;
      Assert.NotEqual(PermissionScopeKind.Server, seedScope);
    }
  }

  [Fact]
  public void Presets_AllPermissionsResolveToBroadestSeedableScope()
  {
    // Presets must be seedable without a concrete resource target, so every preset permission
    // must resolve to a Server or Tenant scope (never a device/group/customer-specific kind).
    // Device permissions resolve to Tenant (not Server) via GetBroadestTenantLegalScope,
    // so presets never grant cross-tenant device access.
    foreach (var (presetName, permissions) in PermissionPresets.All)
    {
      foreach (var permission in permissions)
      {
        var broadest = PermissionCatalog.GetBroadestTenantLegalScope(permission) ??
                       PermissionCatalog.GetBroadestLegalScope(permission);
        Assert.True(
          broadest is PermissionScopeKind.Server or PermissionScopeKind.Tenant,
          $"Preset '{presetName}' permission '{permission}' resolves to broadest scope '{broadest}', which is not seedable without a resource target.");
      }
    }
  }

  [Fact]
  public async Task Replace_OwnOmittingProtectedPermission_ReturnsBadRequest()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedAdmin();
    using var _ = testServer;

    var response = await client.PostAsJsonAsync(
      PaUrl(tenantId, "/replace"),
      new PADtos.ReplacePermissionAssignmentsRequestDto(
        PermissionPrincipalKind.User,
        userId,
        [
          new PADtos.CreatePermissionAssignmentRequestDto(
            PermissionPrincipalKind.User, userId, PermissionNames.TenantRead,
            PermissionEffect.Allow, PermissionScopeKind.Tenant, tenantId, null)
        ]),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  private static string PaUrl(Guid tenantId, string suffix = "") =>
    $"{HttpConstants.V1.PermissionAssignmentsEndpoint}{suffix}?tenantId={tenantId}";

  private async Task<(TestWebServer server, HttpClient client, Guid tenantId, Guid userId)> CreateAuthenticatedAdmin()
  {
    var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"admin-{Guid.NewGuid():N}@t.local",
      PermissionPresets.TenantAdministrator);

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Scope Guard Test PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess, $"PAT creation failed: {patResult.Reason}");

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    return (testServer, httpClient, tenant.Id, user.Id);
  }

  private async Task<PADtos.PermissionAssignmentDto> GetAssignment(
    HttpClient client,
    Guid tenantId,
    Guid principalId,
    string permissionName)
  {
    var response = await client.GetAsync(
      $"{PaUrl(tenantId)}&principalKind=User&principalId={principalId}",
      TestContext.Current.CancellationToken);
    response.EnsureSuccessStatusCode();

    var assignments = await response.Content.ReadFromJsonAsync<PADtos.PermissionAssignmentsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(assignments);

    return Assert.Single(assignments.Items, a => a.PermissionName == permissionName);
  }
}
