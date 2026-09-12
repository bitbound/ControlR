using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.PermissionAssignments;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.AuthorizationChangeLogs;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// V1 authorization change log endpoint audience scoping: holders of server.authorization-logs.read
/// inspect the tenant the required tenantId query parameter selects (any tenant), holders of
/// tenant.authorization-logs.read see only their own tenant, and other principals are forbidden.
/// </summary>
public class AuthorizationChangeLogsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetServerScoped_AsServerAdmin_ReturnsOnlyServerScopedEntries()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenantA, _, serverAdmin, _) = await SetupTenantsWithEntries(testServer);

    var serverEntryId = Guid.NewGuid();
    var tenantEntryId = Guid.NewGuid();

    using (var setupScope = testServer.Services.CreateScope())
    {
      await using var db = setupScope.ServiceProvider.GetRequiredService<ControlR.Web.Server.Data.AppDb>();

      db.AuthorizationChangeLogs.AddRange(
        new AuthorizationChangeLog
        {
          ActionType = AuthorizationChangeLogActions.ServiceAccountCreated,
          ActorPrincipalId = null,
          ActorPrincipalType = AuthorizationChangeLogActorTypes.System,
          CreatedAt = DateTimeOffset.UtcNow,
          Id = serverEntryId,
          OwningTenantId = null,
          TargetId = Guid.NewGuid(),
          TargetType = AuthorizationChangeLogTargetTypes.ServiceAccount
        },
        new AuthorizationChangeLog
        {
          ActionType = AuthorizationChangeLogActions.PermissionAssignmentCreated,
          ActorPrincipalId = Guid.NewGuid(),
          ActorPrincipalType = AuthorizationChangeLogActorTypes.User,
          CreatedAt = DateTimeOffset.UtcNow,
          Id = tenantEntryId,
          OwningTenantId = tenantA.Id,
          TargetId = Guid.NewGuid(),
          TargetType = AuthorizationChangeLogTargetTypes.PermissionAssignment
        });

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using var scope = testServer.Services.CreateScope();

    // The Server Administrator holds server.authorization-logs.read at Server scope, whose catalog
    // description covers server-scoped entries. Carrying a tenant claim must not hide them.
    var controller = await scope.CreateControllerWithUser<AuthorizationChangeLogsController>(serverAdmin);

    var result = await controller.GetServerScoped(
      new AuthorizationChangeLogSearchQueryDto(), TestContext.Current.CancellationToken);

    var response = Assert.IsType<AuthorizationChangeLogsResponseDto>(
      Assert.IsType<OkObjectResult>(result.Result).Value);
    Assert.Contains(response.Items, x => x.Id == serverEntryId);
    Assert.DoesNotContain(response.Items, x => x.Id == tenantEntryId);
    Assert.All(response.Items, x => Assert.Null(x.OwningTenantId));
  }

  [Fact]
  public async Task GetServerScoped_AsTenantAdminWithoutGrant_ReturnsForbid()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (_, _, _, tenantAdminA) = await SetupTenantsWithEntries(testServer);

    using var scope = testServer.Services.CreateScope();
    var controller = await scope.CreateControllerWithUser<AuthorizationChangeLogsController>(tenantAdminA);

    var result = await controller.GetServerScoped(
      new AuthorizationChangeLogSearchQueryDto(), TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetServerScoped_WhenServerServiceAccountHoldsServerRead_ReturnsOnlyServerScopedEntries()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenantA, _, _, _) = await SetupTenantsWithEntries(testServer);

    var serverEntryId = Guid.NewGuid();
    var tenantEntryId = Guid.NewGuid();
    ClaimsPrincipal principal;

    using (var setupScope = testServer.Services.CreateScope())
    {
      // Restricted keeps evaluation on the account's assignment rows. An Unrestricted account
      // bypasses the evaluator entirely and would prove nothing about the grant below.
      var (serviceAccountPrincipal, account, _) = await TestPrincipalHelper.CreateServerServiceAccountAsync(
        setupScope.ServiceProvider,
        $"server-log-reader-{Guid.NewGuid():N}",
        ServiceAccountAccessMode.Restricted,
        TestContext.Current.CancellationToken);
      principal = serviceAccountPrincipal;

      await using var db = setupScope.ServiceProvider.GetRequiredService<ControlR.Web.Server.Data.AppDb>();

      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.ServiceAccount,
        account.Id,
        PermissionNames.ServerAuthorizationLogsRead,
        PermissionScopeKind.Server,
        scopeId: null,
        owningTenantId: null,
        createdBy: null));

      // One row belonging to no tenant (the only kind this route serves) and one tenant-owned row
      // the route must hide.
      db.AuthorizationChangeLogs.AddRange(
        new AuthorizationChangeLog
        {
          ActionType = AuthorizationChangeLogActions.ServiceAccountCreated,
          ActorPrincipalId = null,
          ActorPrincipalType = AuthorizationChangeLogActorTypes.System,
          CreatedAt = DateTimeOffset.UtcNow,
          Id = serverEntryId,
          OwningTenantId = null,
          TargetId = Guid.NewGuid(),
          TargetType = AuthorizationChangeLogTargetTypes.ServiceAccount
        },
        new AuthorizationChangeLog
        {
          ActionType = AuthorizationChangeLogActions.PermissionAssignmentCreated,
          ActorPrincipalId = Guid.NewGuid(),
          ActorPrincipalType = AuthorizationChangeLogActorTypes.User,
          CreatedAt = DateTimeOffset.UtcNow,
          Id = tenantEntryId,
          OwningTenantId = tenantA.Id,
          TargetId = Guid.NewGuid(),
          TargetType = AuthorizationChangeLogTargetTypes.PermissionAssignment
        });

      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testServer.Services.CreateScope())
    {
      var controller = scope.CreateController<AuthorizationChangeLogsController>();
      controller.ControllerContext.HttpContext.User = principal;

      var result = await controller.GetServerScoped(
        new AuthorizationChangeLogSearchQueryDto(), TestContext.Current.CancellationToken);

      var okResult = Assert.IsType<OkObjectResult>(result.Result);
      var response = Assert.IsType<AuthorizationChangeLogsResponseDto>(okResult.Value);
      Assert.NotEmpty(response.Items);
      Assert.Contains(response.Items, x => x.Id == serverEntryId);
      Assert.DoesNotContain(response.Items, x => x.Id == tenantEntryId);
      Assert.All(response.Items, x => Assert.Null(x.OwningTenantId));
    }
  }

  [Fact]
  public async Task Get_AsServerAdmin_ReturnsEntriesFromRequestedTenant()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenantA, tenantB, serverAdmin, _) = await SetupTenantsWithEntries(testServer);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, serverAdmin.Id, serverAdmin.TenantId, "test"));

    // Server-scoped readers may inspect any tenant, including one other than their own.
    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantB.Id}", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.NotEmpty(result.Items);
    Assert.All(result.Items, x => Assert.Equal(tenantB.Id, x.OwningTenantId));
  }

  [Fact]
  public async Task Get_AsTenantReader_ReturnsOnlyOwnTenantEntries()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenantA, tenantB, _, tenantAdminA) = await SetupTenantsWithEntries(testServer);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, tenantAdminA.Id, tenantAdminA.TenantId, "test"));

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.NotEmpty(result.Items);
    Assert.All(result.Items, x => Assert.Equal(tenantA.Id, x.OwningTenantId));

    var crossTenantResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantB.Id}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Forbidden, crossTenantResponse.StatusCode);
  }

  [Fact]
  public async Task Get_AsUnauthorizedUser_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var plainUser = await testServer.Services.CreateTestUser(tenant.Id, $"plain-{Guid.NewGuid():N}@t.local");

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, plainUser.Id, plainUser.TenantId, "test"));

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenant.Id}", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Get_WithActorTypeFilter_RestrictsToMatchingActorTypes()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (tenantA, _, serverAdmin, _) = await SetupTenantsWithEntries(testServer);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, serverAdmin.Id, serverAdmin.TenantId, "test"));

    var userResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}&actorType=user",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
    var userResult = await userResponse.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(userResult);
    Assert.NotEmpty(userResult.Items);
    Assert.All(userResult.Items, x => Assert.Equal(AuthorizationChangeLogActorTypes.User, x.ActorPrincipalType));

    // The setup has no service-account actors; the filtered result must be empty.
    var saResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}&actorType=service-account",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, saResponse.StatusCode);
    var saResult = await saResponse.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(saResult);
    Assert.Empty(saResult.Items);
  }

  [Fact]
  public async Task Get_WithSearchText_MatchesExactAndPartialGuid()
  {
    // Real Postgres exercises the Npgsql translation of Guid?.Value.ToString() in the filter.
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput, useInMemoryDatabase: false);
    var (tenantA, _, serverAdmin, _) = await SetupTenantsWithEntries(testServer);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, serverAdmin.Id, serverAdmin.TenantId, "test"));

    // The tenant-admin assignment created in Setup creates a change-log row with a real target ID.
    var allResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
    var allResult = await allResponse.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(allResult);
    Assert.NotEmpty(allResult.Items);

    var targetId = allResult.Items.First().TargetId;
    Assert.NotNull(targetId);

    // Exact GUID match.
    var exactResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}&searchText={Uri.EscapeDataString(targetId.Value.ToString())}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, exactResponse.StatusCode);
    var exactResult = await exactResponse.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(exactResult);
    Assert.NotEmpty(exactResult.Items);
    Assert.Contains(exactResult.Items, x => x.TargetId == targetId);

    // Partial GUID match (first 8 hex chars).
    var partial = targetId.Value.ToString("D")[..8];
    var partialResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}&searchText={partial}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, partialResponse.StatusCode);
    var partialResult = await partialResponse.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(partialResult);
    Assert.NotEmpty(partialResult.Items);
  }

  [Fact]
  public async Task Get_WithSearchText_PartialGuid_MatchesCaseInsensitively()
  {
    // Real Postgres exercises the Npgsql ILIKE translation of Guid?.Value.ToString().
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput, useInMemoryDatabase: false);
    var (tenantA, _, serverAdmin, _) = await SetupTenantsWithEntries(testServer);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, serverAdmin.Id, serverAdmin.TenantId, "test"));

    var allResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}", TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
    var allResult = await allResponse.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(allResult);
    Assert.NotEmpty(allResult.Items);

    var targetId = allResult.Items.First().TargetId;
    Assert.NotNull(targetId);

    // Uppercase partial GUID must still match (case-insensitive ILIKE).
    var partialUpper = targetId.Value.ToString("D")[..8].ToUpperInvariant();
    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={tenantA.Id}&searchText={partialUpper}",
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var result = await response.Content.ReadFromJsonAsync<AuthorizationChangeLogsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.NotEmpty(result.Items);
    Assert.Contains(result.Items, x => x.TargetId == targetId);
  }

  private async Task<HttpClient> CreatePatClient(TestWebServer testServer, PrincipalDescriptor actor)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Audit Log Test PAT", PersonalAccessTokenPermissionMode.InheritOwner), actor.PrincipalId, actor);
    Assert.True(patResult.IsSuccess);

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }

  private async Task<(Tenant TenantA, Tenant TenantB, AppUser ServerAdmin, AppUser TenantAdminA)> SetupTenantsWithEntries(
    TestWebServer testServer)
  {
    var tenantA = await testServer.Services.CreateTestTenant("Tenant A");
    var tenantB = await testServer.Services.CreateTestTenant("Tenant B");
    await testServer.Services.CreateTestUser(tenantA.Id, email: $"seed-{Guid.NewGuid():N}@t.local");

    var serverAdmin = await testServer.Services.CreateTestUser(tenantA.Id, $"server-admin-{Guid.NewGuid():N}@t.local");
    var tenantAdminA = await testServer.Services.CreateTestUser(
      tenantA.Id, $"tenant-admin-{Guid.NewGuid():N}@t.local", PermissionPresets.TenantAdministrator);
    var tenantAdminB = await testServer.Services.CreateTestUser(
      tenantB.Id, $"tenant-admin-{Guid.NewGuid():N}@t.local", PermissionPresets.TenantAdministrator);
    var targetA = await testServer.Services.CreateTestUser(tenantA.Id, $"target-a-{Guid.NewGuid():N}@t.local");
    var targetB = await testServer.Services.CreateTestUser(tenantB.Id, $"target-b-{Guid.NewGuid():N}@t.local");

    using (var scope = testServer.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<ControlR.Web.Server.Data.AppDb>();
      db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.User,
        serverAdmin.Id,
        PermissionNames.ServerAuthorizationLogsRead,
        PermissionScopeKind.Server,
        null,
        tenantA.Id,
        new PrincipalDescriptor(PrincipalType.User, serverAdmin.Id, tenantA.Id, "test")));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testServer.Services.CreateScope())
    {
      var manager = scope.ServiceProvider.GetRequiredService<IPermissionAssignmentManager>();

      var createA = await manager.Create(
        new InternalDtos.CreatePermissionAssignmentRequestDto(
          PermissionPrincipalKind.User, targetA.Id, PermissionNames.DeviceRead,
          PermissionEffect.Allow, PermissionScopeKind.Tenant, tenantA.Id, null),
        tenantA.Id, new PrincipalDescriptor(PrincipalType.User, tenantAdminA.Id, tenantA.Id, "test"), TestContext.Current.CancellationToken);
      Assert.True(createA.IsSuccess, $"Tenant A assignment failed: {createA.Reason}");

      var createB = await manager.Create(
        new InternalDtos.CreatePermissionAssignmentRequestDto(
          PermissionPrincipalKind.User, targetB.Id, PermissionNames.DeviceRead,
          PermissionEffect.Allow, PermissionScopeKind.Tenant, tenantB.Id, null),
        tenantB.Id, new PrincipalDescriptor(PrincipalType.User, tenantAdminB.Id, tenantB.Id, "test"), TestContext.Current.CancellationToken);
      Assert.True(createB.IsSuccess, $"Tenant B assignment failed: {createB.Reason}");
    }

    return (tenantA, tenantB, serverAdmin, tenantAdminA);
  }
}