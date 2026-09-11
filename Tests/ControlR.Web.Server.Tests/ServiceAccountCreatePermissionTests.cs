using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Api.Internal;
using V1SADtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class ServiceAccountCreatePermissionTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task ServerCreate_WhenAccessModeOmitted_DefaultsToRestricted()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"server-omit-mode-{Guid.NewGuid():N}@t.local");

    await GrantServerPermissions(testServer.Services, user.Id,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    // Omitting accessMode must never grant Unrestricted. The non-nullable enum binds to
    // its default (Restricted), so the created account must be Restricted, not a silent
    // full-bypass escalation.
    var createResponse = await httpClient.PostAsync(
      HttpConstants.V1.ServerServiceAccountsEndpoint,
      new StringContent(
        """{"name":"Omitted Mode Server SA","description":null}""",
        System.Text.Encoding.UTF8,
        "application/json"),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

    var account = await createResponse.Content.ReadFromJsonAsync<V1SADtos.ServerServiceAccountDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(account);
    Assert.Equal(ServiceAccountAccessMode.Restricted, account.AccessMode);
  }

  [Fact]
  public async Task ServerCreate_WhenPermissionWriteUser_CreatesUnrestricted_Succeeds()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"server-perm-write-{Guid.NewGuid():N}@t.local");

    await GrantServerPermissions(testServer.Services, user.Id,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite,
      PermissionNames.ServerPermissionsWrite);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    var createResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.ServerServiceAccountsEndpoint,
      new V1SADtos.CreateServerServiceAccountRequestDto(
        "Unrestricted Server SA",
        Description: null,
        AccessMode: ServiceAccountAccessMode.Unrestricted),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

    var account = await createResponse.Content.ReadFromJsonAsync<V1SADtos.ServerServiceAccountDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(account);
    Assert.Equal(ServiceAccountAccessMode.Unrestricted, account.AccessMode);
  }

  [Fact]
  public async Task ServerCreate_WhenUserHasRotatePermission_CreatesAccountAndAddsCredential()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, user) = await scope.CreateControllerWithTestData<ControlR.Web.Server.Api.V1.ServerServiceAccountsController>(
      userEmail: "server-rotate@t.local");

    await GrantServerPermissions(scope.ServiceProvider, user.Id,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite,
      PermissionNames.ServerServiceAccountsRotateCredentials);

    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var rotateResult = await evaluator.Evaluate(
      CreateUserPrincipal(user.Id, tenant.Id),
      PermissionNames.ServerServiceAccountsRotateCredentials,
      new ResourceDescriptor(PermissionScopeKind.Server),
      TestContext.Current.CancellationToken);

    Assert.True(rotateResult.Allowed,
      "A user with ServerServiceAccountsRotateCredentials must be allowed the server rotate permission.");

    var createResult = await controller.Create(
      new V1SADtos.CreateServerServiceAccountRequestDto(
        "Cred Server SA",
        Description: null,
        AccessMode: ServiceAccountAccessMode.Restricted),
      evaluator,
      TestContext.Current.CancellationToken);

    var createOk = Assert.IsType<CreatedAtActionResult>(createResult.Result);
    var account = Assert.IsType<V1SADtos.ServerServiceAccountDto>(createOk.Value);
    Assert.Empty(account.Credentials);

    var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
    var addCredResult = await controller.AddCredential(
      account.Id,
      new V1SADtos.CreateServiceAccountCredentialRequestDto("Provisioned Key", expiresAt),
      TestContext.Current.CancellationToken);

    var addOk = Assert.IsType<OkObjectResult>(addCredResult.Result);
    var credentialResponse = Assert.IsType<V1SADtos.CreateServiceAccountCredentialResponseDto>(addOk.Value);
    Assert.NotEmpty(credentialResponse.PlainTextSecretKey);
    Assert.Equal("Provisioned Key", credentialResponse.Credential.Name);
    Assert.Equal(expiresAt, credentialResponse.Credential.ExpiresAt);
  }

  [Fact]
  public async Task ServerCreate_WhenWriteOnlyUser_CreatesRestrictedAccount_ButIsDeniedRotatePolicy()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"server-write-only-{Guid.NewGuid():N}@t.local");

    await GrantServerPermissions(testServer.Services, user.Id,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    // Creating a Restricted server account only requires the write permission, which
    // this user holds, so it succeeds.
    var createResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.ServerServiceAccountsEndpoint,
      new V1SADtos.CreateServerServiceAccountRequestDto(
        "Restricted Server SA",
        Description: null,
        AccessMode: ServiceAccountAccessMode.Restricted),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

    var account = await createResponse.Content.ReadFromJsonAsync<V1SADtos.ServerServiceAccountDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(account);
    Assert.Equal(ServiceAccountAccessMode.Restricted, account.AccessMode);

    // The credential endpoint requires the rotate policy, which this user lacks -> HTTP 403.
    var addCredResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{account.Id}/credentials",
      new V1SADtos.CreateServiceAccountCredentialRequestDto("Attempted Key", null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Forbidden, addCredResponse.StatusCode);
  }

  [Fact]
  public async Task ServerCreate_WhenWriteOnlyUser_CreatesUnrestricted_IsDenied()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"server-write-only-{Guid.NewGuid():N}@t.local");

    await GrantServerPermissions(testServer.Services, user.Id,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    // A write-only user (no ServerPermissionsWrite) cannot create an Unrestricted
    // server account, which grants full server bypass.
    var createResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.V1.ServerServiceAccountsEndpoint,
      new V1SADtos.CreateServerServiceAccountRequestDto(
        "Forbidden Server SA",
        Description: null,
        AccessMode: ServiceAccountAccessMode.Unrestricted),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
  }

  [Fact]
  public async Task TenantCreate_WhenUserHasRotatePermission_CreatesAccountAndAddsCredential()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, user) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-rotate@t.local");

    await GrantTenantPermissions(scope.ServiceProvider, tenant.Id, user.Id,
      PermissionNames.ServiceAccountRead,
      PermissionNames.ServiceAccountWrite,
      PermissionNames.ServiceAccountRotateCredentials);

    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var rotateResult = await evaluator.Evaluate(
      CreateUserPrincipal(user.Id, tenant.Id),
      PermissionNames.ServiceAccountRotateCredentials,
      new ResourceDescriptor(PermissionScopeKind.Tenant, Id: tenant.Id, TenantId: tenant.Id),
      TestContext.Current.CancellationToken);

    Assert.True(rotateResult.Allowed,
      "A user with ServiceAccountRotateCredentials must be allowed the rotate permission.");

    var createResult = await controller.Create(
      new InternalDtos.CreateTenantServiceAccountRequestDto("Cred SA", null),
      TestContext.Current.CancellationToken);

    var createOk = Assert.IsType<OkObjectResult>(createResult.Result);
    var account = Assert.IsType<InternalDtos.TenantServiceAccountDto>(createOk.Value);
    Assert.Empty(account.Credentials);

    var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
    var addCredResult = await controller.AddCredential(
      account.Id,
      new InternalDtos.CreateTenantServiceAccountCredentialRequestDto("Provisioned Key", expiresAt),
      TestContext.Current.CancellationToken);

    var addOk = Assert.IsType<OkObjectResult>(addCredResult.Result);
    var credentialResponse = Assert.IsType<InternalDtos.CreateTenantServiceAccountCredentialResponseDto>(addOk.Value);
    Assert.NotEmpty(credentialResponse.PlainTextSecretKey);
    Assert.Equal("Provisioned Key", credentialResponse.Credential.Name);
    Assert.Equal(expiresAt, credentialResponse.Credential.ExpiresAt);
  }

  [Fact]
  public async Task TenantCreate_WhenWriteOnlyUser_CreatesAccount_ButIsDeniedRotatePolicy()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"tenant-write-only-{Guid.NewGuid():N}@t.local");

    await GrantTenantPermissions(testServer.Services, tenant.Id, user.Id,
      PermissionNames.ServiceAccountRead,
      PermissionNames.ServiceAccountWrite);

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    // Create succeeds with only the write permission.
    var createResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.Internal.TenantServiceAccountsEndpoint,
      new InternalDtos.CreateTenantServiceAccountRequestDto("Cred SA", null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

    var account = await createResponse.Content.ReadFromJsonAsync<InternalDtos.TenantServiceAccountDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(account);
    Assert.Empty(account.Credentials);

    // The credential endpoint requires the rotate policy, which this user lacks -> HTTP 403.
    var addCredResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{account.Id}/credentials",
      new InternalDtos.CreateTenantServiceAccountCredentialRequestDto("Attempted Key", null),
      TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Forbidden, addCredResponse.StatusCode);
  }

  private static async Task<HttpClient> CreatePatClient(TestWebServer testServer, PrincipalDescriptor actor)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Service Account Create Test PAT", PersonalAccessTokenPermissionMode.InheritOwner), actor.PrincipalId, actor);
    Assert.True(patResult.IsSuccess, $"PAT creation failed: {patResult.Reason}");

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }

  private static PrincipalDescriptor CreateUserPrincipal(Guid userId, Guid tenantId)
  {
    return new PrincipalDescriptor(
      PrincipalType: PrincipalType.User,
      PrincipalId: userId,
      TenantId: tenantId,
      AuthMethod: "cookie");
  }

  private static async Task GrantServerPermissions(
    IServiceProvider services,
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
        PermissionScopeKind.Server,
        scopeId: null,
        owningTenantId: null,
        createdBy: null));
    }

    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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
}
