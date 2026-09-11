using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Services.Tenants;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


using EPDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectivePermissions;

namespace ControlR.Web.Server.Tests;

public class PermissionManagementIntegrationTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task DeviceGroup_AddAndRemoveMembers_UpdatesMembership()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var device1 = await testServer.Services.CreateTestDevice(tenantId);
    var device2 = await testServer.Services.CreateTestDevice(tenantId);

    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      new InternalDtos.CreateDeviceGroupRequestDto("Member Test Group", null),
      TestContext.Current.CancellationToken);
    var group = await createResponse.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(group);

    var addResponse = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{group.Id}/members",
      new InternalDtos.AddDeviceGroupMembersRequestDto([device1.Id, device2.Id]),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

    var getResponse = await client.GetAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{group.Id}",
      TestContext.Current.CancellationToken);
    var withMembers = await getResponse.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(withMembers);
    Assert.Equal(2, withMembers.Members.Count);

    var removeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{group.Id}/members")
    {
      Content = JsonContent.Create(new InternalDtos.RemoveDeviceGroupMembersRequestDto([device1.Id]))
    }, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

    var afterRemove = await client.GetAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{group.Id}",
      TestContext.Current.CancellationToken);
    var afterRemoveDto = await afterRemove.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(afterRemoveDto);
    Assert.Single(afterRemoveDto.Members);
    Assert.Equal(device2.Id, afterRemoveDto.Members[0].DeviceId);
  }

  [Fact]
  public async Task DeviceGroup_AddMembers_WithGroupScopedPermission_AuthorizesOnlyTargetGroup()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var device = await testServer.Services.CreateTestDevice(tenantId);
    var authorizedGroup = await CreateDeviceGroup(client, "Authorized Device Group");
    var unauthorizedGroup = await CreateDeviceGroup(client, "Unauthorized Device Group");

    await ReplaceGroupAssignment(
      testServer.Services,
      userId,
      tenantId,
      PermissionNames.DeviceGroupAssignDevices,
      PermissionScopeKind.DeviceGroup,
      authorizedGroup.Id);

    var authorizedResponse = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{authorizedGroup.Id}/members",
      new InternalDtos.AddDeviceGroupMembersRequestDto([device.Id]),
      TestContext.Current.CancellationToken);
    var unauthorizedResponse = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{unauthorizedGroup.Id}/members",
      new InternalDtos.AddDeviceGroupMembersRequestDto([device.Id]),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, authorizedResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, unauthorizedResponse.StatusCode);
  }

  [Fact]
  public async Task DeviceGroup_CreateGetUpdateDelete_CompletesFullCycle()
  {
    var (testServer, client, _, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      new InternalDtos.CreateDeviceGroupRequestDto("Production Servers", "Main production fleet"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);
    Assert.Equal("Production Servers", created.Name);
    Assert.Equal("Main production fleet", created.Description);
    Assert.NotEqual(Guid.Empty, created.Id);
    Assert.Empty(created.Members);

    var getResponse = await client.GetAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{created.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    var fetched = await getResponse.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(fetched);
    Assert.Equal(created.Id, fetched.Id);

    var updateResponse = await client.PutAsJsonAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{created.Id}",
      new InternalDtos.UpdateDeviceGroupRequestDto("Staging Servers", "Staging fleet"),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    var updated = await updateResponse.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(updated);
    Assert.Equal("Staging Servers", updated.Name);
    Assert.Equal("Staging fleet", updated.Description);

    var deleteResponse = await client.DeleteAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{created.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

    var getAfterDelete = await client.GetAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{created.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
  }

  [Fact]
  public async Task DeviceGroup_Delete_CascadesPermissionAssignments()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var createGroupResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      new InternalDtos.CreateDeviceGroupRequestDto("Cascade Test Group", null),
      TestContext.Current.CancellationToken);
    var group = await createGroupResponse.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(group);

    var createAssignmentResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userId,
        "device.read",
        PermissionEffect.Allow,
        PermissionScopeKind.DeviceGroup,
        group.Id,
        null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, createAssignmentResponse.StatusCode);

    var deleteResponse = await client.DeleteAsync(
      $"{HttpConstants.Internal.DeviceGroupsEndpoint}/{group.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

    using var scope = testServer.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var remaining = await db.PermissionAssignments
      .CountAsync(x => x.ScopeKind == PermissionScopeKind.DeviceGroup && x.ScopeId == group.Id,
        TestContext.Current.CancellationToken);
    Assert.Equal(0, remaining);
  }

  [Fact]
  public async Task DeviceGroup_GetAll_ReturnsCreatedGroups()
  {
    var (testServer, client, _, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    await client.PostAsJsonAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      new InternalDtos.CreateDeviceGroupRequestDto("Group A", null),
      TestContext.Current.CancellationToken);
    await client.PostAsJsonAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      new InternalDtos.CreateDeviceGroupRequestDto("Group B", null),
      TestContext.Current.CancellationToken);

    var response = await client.GetAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var groups = await response.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDto[]>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(groups);
    Assert.Equal(2, groups.Length);
  }

  [Fact]
  public async Task DeviceGroup_Unauthenticated_Returns401()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var client = testServer.Factory.CreateClient();

    var response = await client.GetAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task EffectivePermission_Query_MovedUserFromOtherTenant_Returns404()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    // Create a user in another tenant.
    var otherTenant = await testServer.Services.CreateTestTenant();
    var otherUser = await testServer.Services.CreateTestUser(
      otherTenant.Id, $"other-{Guid.NewGuid():N}@t.local");

    // Querying that user from this tenant's context should return 404.
    var queryResponse = await client.GetAsync(
      EffectivePermissionUrl(PermissionPrincipalKind.User, otherUser.Id, PermissionNames.DeviceRead, tenantId),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NotFound, queryResponse.StatusCode);
  }

  [Fact]
  public async Task EffectivePermission_Query_ReturnsAllowedForTenantAdmin()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var queryResponse = await client.GetAsync(
      EffectivePermissionUrl(PermissionPrincipalKind.User, userId, "tenant.permissions.read", tenantId),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
    var result = await queryResponse.Content.ReadFromJsonAsync<EPDtos.EffectivePermissionQueryResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.True(result.IsAllowed);
  }

  [Fact]
  public async Task EffectivePermission_Query_ReturnsAllowedForTenantServiceAccount()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    // Create a tenant service account and grant it device.read at tenant scope.
    var createAccountResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.TenantServiceAccountsEndpoint,
      new InternalDtos.CreateTenantServiceAccountRequestDto("Effective Query SA", null),
      TestContext.Current.CancellationToken);
    var account = await createAccountResponse.Content.ReadFromJsonAsync<InternalDtos.TenantServiceAccountDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(account);

    var createAssignmentResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.ServiceAccount,
        account.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, createAssignmentResponse.StatusCode);

    var queryResponse = await client.GetAsync(
      EffectivePermissionUrl(PermissionPrincipalKind.ServiceAccount, account.Id, PermissionNames.DeviceRead, tenantId),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
    var result = await queryResponse.Content.ReadFromJsonAsync<EPDtos.EffectivePermissionQueryResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.True(result.IsAllowed);
  }

  [Fact]
  public async Task EffectivePermission_Query_ReturnsAllowedForUserGroup()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    // Create a user group and grant it device.read at tenant scope.
    var createGroupResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      new InternalDtos.CreateUserGroupRequestDto("Effective Query Group", null),
      TestContext.Current.CancellationToken);
    var group = await createGroupResponse.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(group);

    var createAssignmentResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.UserGroup,
        group.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, createAssignmentResponse.StatusCode);

    // Query the group's effective permission — should be allowed.
    var queryResponse = await client.GetAsync(
      EffectivePermissionUrl(PermissionPrincipalKind.UserGroup, group.Id, PermissionNames.DeviceRead, tenantId),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
    var result = await queryResponse.Content.ReadFromJsonAsync<EPDtos.EffectivePermissionQueryResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.True(result.IsAllowed);
  }

  [Fact]
  public async Task EffectivePermission_Query_ReturnsDeniedForNonAdmin()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var normalUser = await testServer.Services.CreateTestUser(
      tenantId, $"normal-{Guid.NewGuid():N}@t.local");

    var queryResponse = await client.GetAsync(
      EffectivePermissionUrl(PermissionPrincipalKind.User, normalUser.Id, "tenant.permissions.write", tenantId),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
    var result = await queryResponse.Content.ReadFromJsonAsync<EPDtos.EffectivePermissionQueryResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.False(result.IsAllowed);
  }

  [Fact]
  public async Task EffectivePermission_Query_UnsupportedPrincipalKind_ReturnsBadRequest()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var queryResponse = await client.GetAsync(
      EffectivePermissionUrl(PermissionPrincipalKind.PersonalAccessToken, Guid.NewGuid(), PermissionNames.DeviceRead, tenantId),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, queryResponse.StatusCode);
  }

  [Fact]
  public async Task PermissionAssignment_CreateAndGetByPrincipal_ReturnsAssignment()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userId,
        "device.read",
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        "Test assignment"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);
    Assert.Equal("device.read", created.PermissionName);
    Assert.Equal(PermissionEffect.Allow, created.Effect);
    Assert.Equal(PermissionScopeKind.Tenant, created.ScopeKind);
    Assert.Equal(tenantId, created.ScopeId);
    Assert.True(created.IsEnabled);

    var getResponse = await client.GetAsync(
      $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}?principalKind=User&principalId={userId}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    var assignments = await getResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto[]>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(assignments);
    Assert.Contains(assignments, a => a.Id == created.Id);
  }

  [Fact]
  public async Task PermissionAssignment_Create_ByServiceAccount_RecordsServiceAccountActor()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    using var httpClient = await testServer.GetHttpClient();
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var targetUser = await services.CreateTestUser(tenant.Id, $"target-{Guid.NewGuid():N}@t.local");

    var saManager = services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForTenant(
      $"actor-sa-{Guid.NewGuid():N}", null, tenant.Id, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess, saResult.Reason);
    var credResult = await saManager.AddCredentialForTenant(
      saResult.Value.Id, tenant.Id, "Actor Cred", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess, credResult.Reason);

    // Grant the service account tenant.permissions.write so it can create assignments.
    using (var scope = services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.ServiceAccount,
        saResult.Value.Id,
        PermissionNames.TenantPermissionsWrite,
        PermissionScopeKind.Tenant,
        tenant.Id,
        tenant.Id,
        createdBy: null));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var createResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        targetUser.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenant.Id,
        null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);

    using (var scope = services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var log = await db.AuthorizationChangeLogs
        .IgnoreQueryFilters()
        .SingleAsync(x => x.ActionType == AuthorizationChangeLogActions.PermissionAssignmentCreated &&
                          x.TargetId == created.Id, TestContext.Current.CancellationToken);
      Assert.Equal(AuthorizationChangeLogActorTypes.ServiceAccount, log.ActorPrincipalType);

      var assignment = await db.PermissionAssignments
        .IgnoreQueryFilters()
        .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
      Assert.Equal(AuthorizationChangeLogActorTypes.ServiceAccount, assignment.CreatedByPrincipalType);
    }
  }

  [Fact]
  public async Task PermissionAssignment_Create_ByUser_RecordsUserActor()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var targetUser = await testServer.Services.CreateTestUser(
      tenantId, $"target-{Guid.NewGuid():N}@t.local");

    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        targetUser.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);

    using (var scope = testServer.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var log = await db.AuthorizationChangeLogs
        .IgnoreQueryFilters()
        .SingleAsync(x => x.ActionType == AuthorizationChangeLogActions.PermissionAssignmentCreated &&
                          x.TargetId == created.Id, TestContext.Current.CancellationToken);
      Assert.Equal(AuthorizationChangeLogActorTypes.User, log.ActorPrincipalType);

      var assignment = await db.PermissionAssignments
        .IgnoreQueryFilters()
        .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
      Assert.Equal(AuthorizationChangeLogActorTypes.User, assignment.CreatedByPrincipalType);
    }
  }

  [Fact]
  public async Task PermissionAssignment_Create_RespectsIsEnabledRequest()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    // Creating with IsEnabled=false must persist a disabled assignment
    // previously CreateGrant hardcoded IsEnabled=true, silently ignoring the switch).
    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userId,
        "device.read",
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        "Disabled on create",
        IsEnabled: false),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);
    Assert.False(created.IsEnabled);

    // The default (omitted) remains enabled.
    var defaultResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userId,
        "device.overview.read",
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
    var defaultAssignment = await defaultResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(defaultAssignment);
    Assert.True(defaultAssignment.IsEnabled);
  }

  [Fact]
  public async Task PermissionAssignment_Delete_RemovesAssignment()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userId,
        PermissionNames.DeviceRead,
        PermissionEffect.Deny,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    var created = await createResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);

    var deleteResponse = await client.DeleteAsync(
      $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/{created.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

    var getResponse = await client.GetAsync(
      $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}?principalKind=User&principalId={userId}",
      TestContext.Current.CancellationToken);
    var assignments = await getResponse.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto[]>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(assignments);
    Assert.DoesNotContain(assignments, a => a.Id == created.Id);
  }

  [Fact]
  public async Task TenantDeletion_CascadesPermissionAssignments()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput, useInMemoryDatabase: false);
    var services = testServer.Services;

    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(
      tenant.Id, $"cascade-{Guid.NewGuid():N}@t.local", PermissionPresets.TenantAdministrator);

    using (var scope = services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.PermissionAssignments.Add(new Data.Entities.PermissionAssignment
      {
        PrincipalKind = PermissionPrincipalKind.User,
        PrincipalId = user.Id,
        PermissionName = "device.read",
        Effect = PermissionEffect.Allow,
        ScopeKind = PermissionScopeKind.Tenant,
        ScopeId = tenant.Id,
        IsEnabled = true,
        OwningTenantId = tenant.Id,
        CreatedByPrincipalType = "user",
        CreatedByPrincipalId = user.Id.ToString()
      });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var count = await db.PermissionAssignments
        .IgnoreQueryFilters()
        .CountAsync(x => x.OwningTenantId == tenant.Id, TestContext.Current.CancellationToken);
      Assert.True(count > 0);
    }

    using (var scope = services.CreateScope())
    {
      var tenantProvisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
      await tenantProvisioning.DeleteTenant(tenant.Id, TestContext.Current.CancellationToken);
    }

    using (var scope = services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      var remaining = await db.PermissionAssignments
        .IgnoreQueryFilters()
        .CountAsync(x => x.OwningTenantId == tenant.Id, TestContext.Current.CancellationToken);
      Assert.Equal(0, remaining);
    }
  }

  [Fact]
  public async Task UserGroup_AddAndRemoveMembers_UpdatesMembership()
  {
    var (testServer, client, tenantId, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var memberUser = await testServer.Services.CreateTestUser(
      tenantId, $"member-{Guid.NewGuid():N}@t.local");

    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      new InternalDtos.CreateUserGroupRequestDto("Member Test Group", null),
      TestContext.Current.CancellationToken);
    var group = await createResponse.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(group);

    var addResponse = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{group.Id}/members",
      new InternalDtos.AddUserGroupMembersRequestDto([memberUser.Id]),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

    var getResponse = await client.GetAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{group.Id}",
      TestContext.Current.CancellationToken);
    var withMembers = await getResponse.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(withMembers);
    Assert.Single(withMembers.Members);
    Assert.Equal(memberUser.Id, withMembers.Members[0].UserId);

    var removeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{group.Id}/members")
    {
      Content = JsonContent.Create(new InternalDtos.RemoveUserGroupMembersRequestDto([memberUser.Id]))
    }, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

    var afterRemove = await client.GetAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{group.Id}",
      TestContext.Current.CancellationToken);
    var afterRemoveDto = await afterRemove.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(afterRemoveDto);
    Assert.Empty(afterRemoveDto.Members);
  }

  [Fact]
  public async Task UserGroup_AddMembers_WithGroupScopedPermission_AuthorizesOnlyTargetGroup()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var memberUser = await testServer.Services.CreateTestUser(
      tenantId, $"member-{Guid.NewGuid():N}@t.local");
    var authorizedGroup = await CreateUserGroup(client, "Authorized User Group");
    var unauthorizedGroup = await CreateUserGroup(client, "Unauthorized User Group");

    await ReplaceGroupAssignment(
      testServer.Services,
      userId,
      tenantId,
      PermissionNames.UserGroupAssignUsers,
      PermissionScopeKind.UserGroup,
      authorizedGroup.Id);

    var authorizedResponse = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{authorizedGroup.Id}/members",
      new InternalDtos.AddUserGroupMembersRequestDto([memberUser.Id]),
      TestContext.Current.CancellationToken);
    var unauthorizedResponse = await client.PostAsJsonAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{unauthorizedGroup.Id}/members",
      new InternalDtos.AddUserGroupMembersRequestDto([memberUser.Id]),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, authorizedResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, unauthorizedResponse.StatusCode);
  }

  [Fact]
  public async Task UserGroup_CreateGetUpdateDelete_CompletesFullCycle()
  {
    var (testServer, client, _, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var createResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      new InternalDtos.CreateUserGroupRequestDto("Engineering", "Engineering team"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    var created = await createResponse.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);
    Assert.Equal("Engineering", created.Name);
    Assert.Equal("Engineering team", created.Description);
    Assert.NotEqual(Guid.Empty, created.Id);
    Assert.Empty(created.Members);

    var getResponse = await client.GetAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{created.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

    var updateResponse = await client.PutAsJsonAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{created.Id}",
      new InternalDtos.UpdateUserGroupRequestDto("Platform Engineering", "Platform team"),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    var updated = await updateResponse.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(updated);
    Assert.Equal("Platform Engineering", updated.Name);

    var deleteResponse = await client.DeleteAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{created.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

    var getAfterDelete = await client.GetAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{created.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
  }

  [Fact]
  public async Task UserGroup_Delete_CascadesPermissionAssignments()
  {
    var (testServer, client, tenantId, userId) = await CreateAuthenticatedServer();
    using var _ = testServer;

    var createGroupResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      new InternalDtos.CreateUserGroupRequestDto("Cascade Test User Group", null),
      TestContext.Current.CancellationToken);
    var group = await createGroupResponse.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(group);

    var createAssignmentResponse = await client.PostAsJsonAsync(
      HttpConstants.Internal.PermissionAssignmentsEndpoint,
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.UserGroup,
        group.Id,
        "device.read",
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, createAssignmentResponse.StatusCode);

    var deleteResponse = await client.DeleteAsync(
      $"{HttpConstants.Internal.UserGroupsEndpoint}/{group.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

    using var scope = testServer.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var remaining = await db.PermissionAssignments
      .CountAsync(x => x.PrincipalKind == PermissionPrincipalKind.UserGroup && x.PrincipalId == group.Id,
        TestContext.Current.CancellationToken);
    Assert.Equal(0, remaining);
  }

  [Fact]
  public async Task UserGroup_GetAll_ReturnsCreatedGroups()
  {
    var (testServer, client, _, _) = await CreateAuthenticatedServer();
    using var _ = testServer;

    await client.PostAsJsonAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      new InternalDtos.CreateUserGroupRequestDto("Team A", null),
      TestContext.Current.CancellationToken);
    await client.PostAsJsonAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      new InternalDtos.CreateUserGroupRequestDto("Team B", null),
      TestContext.Current.CancellationToken);

    var response = await client.GetAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var groups = await response.Content.ReadFromJsonAsync<InternalDtos.UserGroupDto[]>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(groups);
    Assert.Equal(2, groups.Length);
  }

  private static async Task<InternalDtos.DeviceGroupDetailDto> CreateDeviceGroup(
    HttpClient client,
    string name)
  {
    var response = await client.PostAsJsonAsync(
      HttpConstants.Internal.DeviceGroupsEndpoint,
      new InternalDtos.CreateDeviceGroupRequestDto(name, null),
      TestContext.Current.CancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<InternalDtos.DeviceGroupDetailDto>(
      TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("Device group response was empty.");
  }

  private static async Task<InternalDtos.UserGroupDetailDto> CreateUserGroup(
    HttpClient client,
    string name)
  {
    var response = await client.PostAsJsonAsync(
      HttpConstants.Internal.UserGroupsEndpoint,
      new InternalDtos.CreateUserGroupRequestDto(name, null),
      TestContext.Current.CancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<InternalDtos.UserGroupDetailDto>(
      TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("User group response was empty.");
  }

  private static string EffectivePermissionUrl(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    string permissionName,
    Guid tenantId)
  {
    return $"{HttpConstants.V1.EffectivePermissionsEndpoint}/{principalId}" +
           $"?tenantId={tenantId}&principalKind={principalKind}" +
           $"&permissionName={Uri.EscapeDataString(permissionName)}" +
           $"&scopeKind={PermissionScopeKind.Tenant}&scopeId={tenantId}";
  }

  private static async Task ReplaceGroupAssignment(
    IServiceProvider services,
    Guid userId,
    Guid tenantId,
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid scopeId)
  {
    using var scope = services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var existingAssignments = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.User &&
                  x.PrincipalId == userId &&
                  x.PermissionName == permissionName)
      .ToListAsync(TestContext.Current.CancellationToken);
    db.PermissionAssignments.RemoveRange(existingAssignments);
    db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      userId,
      permissionName,
      scopeKind,
      scopeId,
      tenantId,
      new PrincipalDescriptor(PrincipalType.User, userId, tenantId, "test")));
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private async Task<(TestWebServer server, HttpClient client, Guid tenantId, Guid userId)> CreateAuthenticatedServer()
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
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Integration Test PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess, $"PAT creation failed: {patResult.Reason}");

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    return (testServer, httpClient, tenant.Id, user.Id);
  }
}
