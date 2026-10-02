using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

public class V1LogonTokenPermissionsTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateForExternal_CreatorLacksPermission_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(tenant.Id, $"limited-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testServer, tenant.Id, user.Id, PermissionNames.DeviceLogonTokenCreate);
    await SeedAssignment(testServer, tenant.Id, user.Id, PermissionNames.DeviceRead);

    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Limited PAT", PersonalAccessTokenPermissionMode.InheritOwner), user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "limited-user",
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceTerminalUse]);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateForExternal_DeviceReadForced_WhenOnlyOtherPermissionRequested()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("ForcedReadSA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "forced-read-user",
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceTerminalUse]);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var tokenResult = await response.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(tokenResult);

    var tokenId = LogonTokenTestHelper.ParseTokenId(tokenResult.Token!);

    using var scope = testServer.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var grantNames = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenId)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, grantNames.Count);
    Assert.Contains(PermissionNames.DeviceTerminalUse, grantNames);
    Assert.Contains(PermissionNames.DeviceRead, grantNames);
  }

  [Fact]
  public async Task CreateForExternal_EmptyPermissions_BaselineDefaults()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("EmptyPermSA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "empty-perm-user",
      ExpirationMinutes: 15,
      Permissions: []);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external", request, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var tokenResult = await response.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(tokenResult);

    var tokenId = LogonTokenTestHelper.ParseTokenId(tokenResult.Token!);

    using var scope = testServer.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var grantNames = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenId)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, grantNames.Count);
    Assert.Contains(PermissionNames.DeviceRead, grantNames);
    Assert.Contains(PermissionNames.DeviceOverviewRead, grantNames);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlConnect, grantNames);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlInteract, grantNames);
  }

  [Fact]
  public async Task CreateForExternal_ExplicitDeny_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id, $"denied-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);

    using (var scope = testServer.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
      db.PermissionAssignments.Add(new PermissionAssignment
      {
        PrincipalKind = PermissionPrincipalKind.User,
        PrincipalId = user.Id,
        PermissionName = PermissionNames.DeviceTerminalUse,
        Effect = PermissionEffect.Deny,
        ScopeKind = PermissionScopeKind.Tenant,
        ScopeId = tenant.Id,
        IsEnabled = true,
        OwningTenantId = tenant.Id,
        CreatedByPrincipalType = "user",
        CreatedByPrincipalId = user.Id.ToString()
      });
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Denied PAT", PersonalAccessTokenPermissionMode.InheritOwner), user.Id, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "denied-user",
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceTerminalUse]);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateForExternal_OmittedPermissions_BaselineDefaults()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("BaselineSA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "baseline-user",
      ExpirationMinutes: 15);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external", request, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var tokenResult = await response.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(tokenResult);

    var tokenId = LogonTokenTestHelper.ParseTokenId(tokenResult.Token!);

    using var scope = testServer.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var grantNames = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenId)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, grantNames.Count);
    Assert.Contains(PermissionNames.DeviceRead, grantNames);
    Assert.Contains(PermissionNames.DeviceOverviewRead, grantNames);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlConnect, grantNames);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlInteract, grantNames);
  }

  [Fact]
  public async Task CreateForExternal_PermissionsAtLimit_ReturnsOk()
  {
    // The boundary case: exactly PermissionsMaxLength items is still valid and reaches the
    // service (which dedupes and writes the grants), proving model binding accepts the cap and
    // the service guard (Count > limit) does not reject it.
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("AtLimitSA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    // The catalog has fewer than PermissionsMaxLength entries, so duplicates are required to
    // hit the limit. Duplicates are deduped by PrepareScopes; the unioned DeviceRead scope is
    // the only one the creator actually holds.
    var atLimit = Enumerable
      .Repeat(PermissionNames.DeviceRead, DtoLimits.PermissionsMaxLength)
      .ToArray();

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "at-limit-user",
      ExpirationMinutes: 15,
      Permissions: atLimit);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var tokenResult = await response.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(tokenResult);

    var tokenId = LogonTokenTestHelper.ParseTokenId(tokenResult.Token!);

    using var scope = testServer.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var grantNames = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenId)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Contains(PermissionNames.DeviceRead, grantNames);
  }

  [Fact]
  public async Task CreateForExternal_PermissionsOverLimit_ReturnsBadRequest()
  {
    // Pins that [MaxLength(PermissionsMaxLength)] on the V1 DTO is enforced by ASP.NET model
    // binding (MaxLengthAttribute validates the Count of an IReadOnlyList<T> via reflection),
    // so an over-limit Permissions list never reaches the service. The service guard in
    // LogonTokenScopeService.PrepareScopes remains load-bearing for the internal
    // LogonTokenRequestDto path, whose Scopes property carries no [MaxLength].
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("OverLimitSA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var overLimit = Enumerable
      .Repeat(PermissionNames.DeviceRead, DtoLimits.PermissionsMaxLength + 1)
      .ToArray();

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "over-limit-user",
      ExpirationMinutes: 15,
      Permissions: overLimit);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateForExternal_SameCorrelationId_MultipleTokens_IndependentGrants()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("ConcurrencySA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var requestA = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "shared-user",
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceTerminalUse]);

    var responseA = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external", requestA, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
    var resultA = await responseA.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);

    var requestB = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "shared-user",
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceRemoteControlConnect]);

    var responseB = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external", requestB, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
    var resultB = await responseB.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);

    Assert.NotNull(resultA);
    Assert.NotNull(resultB);

    var tokenIdA = LogonTokenTestHelper.ParseTokenId(resultA.Token!);
    var tokenIdB = LogonTokenTestHelper.ParseTokenId(resultB.Token!);

    using var scope = testServer.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();

    var grantsA = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenIdA)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    var grantsB = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenIdB)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Contains(PermissionNames.DeviceTerminalUse, grantsA);
    Assert.Contains(PermissionNames.DeviceRead, grantsA);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlConnect, grantsA);

    Assert.Contains(PermissionNames.DeviceRemoteControlConnect, grantsB);
    Assert.Contains(PermissionNames.DeviceRead, grantsB);
    Assert.DoesNotContain(PermissionNames.DeviceTerminalUse, grantsB);

    var extUser = await db.Users.FirstAsync(
      x => x.UserName == "ext-shared-user" && x.TenantId == tenant.Id,
      TestContext.Current.CancellationToken);
    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var principalA = new PrincipalDescriptor(
      PrincipalType.User, extUser.Id, tenant.Id,
      PrincipalClaimValues.LogonTokenMethod, tokenIdA,
      CredentialType.LogonToken, device.Id);

    var principalB = new PrincipalDescriptor(
      PrincipalType.User, extUser.Id, tenant.Id,
      PrincipalClaimValues.LogonTokenMethod, tokenIdB,
      CredentialType.LogonToken, device.Id);

    Assert.True((await evaluator.Evaluate(
      principalA, PermissionNames.DeviceTerminalUse, resource, TestContext.Current.CancellationToken)).Allowed);
    Assert.False((await evaluator.Evaluate(
      principalA, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken)).Allowed);

    Assert.True((await evaluator.Evaluate(
      principalB, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken)).Allowed);
    Assert.False((await evaluator.Evaluate(
      principalB, PermissionNames.DeviceTerminalUse, resource, TestContext.Current.CancellationToken)).Allowed);
  }

  [Fact]
  public async Task CreateForExternal_UnknownPermissionName_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("UnknownPermSA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "unknown-perm-user",
      ExpirationMinutes: 15,
      Permissions: ["device.nonexistent"]);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    Assert.Contains("Unknown permission name: device.nonexistent", body);
  }

  [Fact]
  public async Task CreateForExternal_WithPermissions_GrantsOnToken_EvaluationCorrect()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = await testServer.GetHttpClient();

    var tenant = await testServer.Services.CreateTestTenant();
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var saManager = testServer.Services.GetRequiredService<IServiceAccountManager>();
    var saResult = await saManager.CreateForServer("PermTestSA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await saManager.AddCredentialForServer(
      saResult.Value.Id, "Test Credential", expiresAt: null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      credResult.Value.PlainTextSecretKey);

    var request = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserCorrelationId: "perm-test-user",
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceTerminalUse]);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/external",
      request,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var tokenResult = await response.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(tokenResult);
    Assert.NotNull(tokenResult.Token);

    var tokenId = LogonTokenTestHelper.ParseTokenId(tokenResult.Token);

    using var scope = testServer.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var grantNames = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenId)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Contains(PermissionNames.DeviceTerminalUse, grantNames);
    Assert.Contains(PermissionNames.DeviceRead, grantNames);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlConnect, grantNames);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlInteract, grantNames);

    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var extUser = await db.Users.FirstAsync(
      x => x.UserName == "ext-perm-test-user" && x.TenantId == tenant.Id,
      TestContext.Current.CancellationToken);

    var principal = new PrincipalDescriptor(
      PrincipalType: PrincipalType.User,
      PrincipalId: extUser.Id,
      TenantId: tenant.Id,
      AuthMethod: PrincipalClaimValues.LogonTokenMethod,
      CredentialId: tokenId,
      CredentialType: CredentialType.LogonToken,
      DeviceScopeId: device.Id);

    var resource = new ResourceDescriptor(PermissionScopeKind.Device, device.Id, tenant.Id);

    var terminalEval = await evaluator.Evaluate(
      principal, PermissionNames.DeviceTerminalUse, resource, TestContext.Current.CancellationToken);
    Assert.True(terminalEval.Allowed, "Terminal use should be allowed");

    var rcEval = await evaluator.Evaluate(
      principal, PermissionNames.DeviceRemoteControlConnect, resource, TestContext.Current.CancellationToken);
    Assert.False(rcEval.Allowed, "Remote control connect should be denied");
  }

  [Fact]
  public async Task CreateForUser_CreatorLacksPermission_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var caller = await testServer.Services.CreateTestUser(tenant.Id, $"limited-{Guid.NewGuid():N}@t.local");
    var targetUser = await testServer.Services.CreateTestUser(tenant.Id, $"target-{Guid.NewGuid():N}@t.local");

    await SeedAssignment(testServer, tenant.Id, caller.Id, PermissionNames.DeviceLogonTokenCreate);
    await SeedAssignment(testServer, tenant.Id, caller.Id, PermissionNames.DeviceRead);

    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("LimitedUser PAT", PersonalAccessTokenPermissionMode.InheritOwner), caller.Id, new PrincipalDescriptor(PrincipalType.User, caller.Id, caller.TenantId, "test"));
    Assert.True(patResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var request = new V1Dtos.CreateLogonTokenForUserRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserId: targetUser.Id,
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceTerminalUse]);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/user", request, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateForUser_WithPermissions_GrantsOnToken()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    using var httpClient = testServer.Factory.CreateClient();

    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var caller = await testServer.Services.CreateTestUser(
      tenant.Id, $"caller-{Guid.NewGuid():N}@t.local", PermissionPresets.DeviceSuperUser);
    var targetUser = await testServer.Services.CreateTestUser(tenant.Id, $"target-{Guid.NewGuid():N}@t.local");
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("UserEndpoint PAT", PersonalAccessTokenPermissionMode.InheritOwner), caller.Id, new PrincipalDescriptor(PrincipalType.User, caller.Id, caller.TenantId, "test"));
    Assert.True(patResult.IsSuccess);

    httpClient.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var request = new V1Dtos.CreateLogonTokenForUserRequestDto(
      DeviceId: device.Id,
      TenantId: tenant.Id,
      UserId: targetUser.Id,
      ExpirationMinutes: 15,
      Permissions: [PermissionNames.DeviceTerminalUse]);

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.LogonTokensEndpoint}/user", request, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var tokenResult = await response.Content
      .ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(tokenResult);

    var tokenId = LogonTokenTestHelper.ParseTokenId(tokenResult.Token!);

    using var scope = testServer.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var grantNames = await db.PermissionAssignments
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.LogonToken && x.PrincipalId == tokenId)
      .Select(x => x.PermissionName)
      .ToListAsync(TestContext.Current.CancellationToken);

    Assert.Contains(PermissionNames.DeviceTerminalUse, grantNames);
    Assert.Contains(PermissionNames.DeviceRead, grantNames);
    Assert.DoesNotContain(PermissionNames.DeviceRemoteControlConnect, grantNames);
  }

  private static async Task SeedAssignment(
    TestWebServer testServer,
    Guid tenantId,
    Guid userId,
    string permissionName)
  {
    using var scope = testServer.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(new PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = userId,
      PermissionName = permissionName,
      Effect = PermissionEffect.Allow,
      ScopeKind = PermissionScopeKind.Tenant,
      ScopeId = tenantId,
      OwningTenantId = tenantId,
      IsEnabled = true
    });
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }
}
