using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using DODtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;

namespace ControlR.Web.Server.Tests.V1;

public class DeploymentOptionsV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AgentInstaller_CanCreateInstallerKeyWithoutTenantAdminReads()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(
      tenant.Id,
      email: $"seed-{Guid.NewGuid():N}@t.local");
    var installer = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"installer-{Guid.NewGuid():N}@t.local",
      PermissionPresets.AgentInstaller);
    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, installer.Id, installer.TenantId, "test"));

    var createKeyResponse = await httpClient.PostAsJsonAsync(
      HttpConstants.Internal.InstallerKeysEndpoint,
      new InternalDtos.CreateInstallerKeyRequestDto(InstallerKeyType.Persistent),
      TestContext.Current.CancellationToken);
    var customersResponse = await httpClient.GetAsync(
      $"{HttpConstants.V1.CustomersEndpoint}?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);
    var tenantSettingsResponse = await httpClient.GetAsync(
      HttpConstants.Internal.TenantSettingsEndpoint,
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, createKeyResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, customersResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, tenantSettingsResponse.StatusCode);
  }

  [Fact]
  public async Task GetTagCapability_AgentInstallerWithoutTagPermission_DeniesNewDevice()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(
      tenant.Id,
      email: $"seed-{Guid.NewGuid():N}@t.local");
    var installer = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"installer-{Guid.NewGuid():N}@t.local",
      PermissionPresets.AgentInstaller);
    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, installer.Id, installer.TenantId, "test"));

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.DeploymentOptionsEndpoint}/tag-capability?tenantId={tenant.Id}",
      new DODtos.DeploymentTagCapabilityRequestDto(null, null),
      TestContext.Current.CancellationToken);
    var result = await response.Content.ReadFromJsonAsync<DODtos.DeploymentTagCapabilityResponseDto>(
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.NotNull(result);
    Assert.False(result.Allowed);
  }

  [Fact]
  public async Task GetTagCapability_DeviceScopedGrant_AllowsPredeterminedTarget()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var seed = await testServer.Services.CreateTestUser(
      tenant.Id,
      email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"tags-{Guid.NewGuid():N}@t.local");
    var device = await testServer.Services.CreateTestDevice(tenant.Id);

    using (var scope = testServer.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<Data.AppDb>();
      db.PermissionAssignments.AddRange(
        CreateAssignment(user.Id, PermissionNames.AgentInstall, tenant.Id, PermissionScopeKind.Tenant, tenant.Id),
        CreateAssignment(user.Id, PermissionNames.DeviceTagsWrite, tenant.Id, PermissionScopeKind.Device, device.Id));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    var allowedResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.DeploymentOptionsEndpoint}/tag-capability?tenantId={tenant.Id}",
      new DODtos.DeploymentTagCapabilityRequestDto(device.Id, null),
      TestContext.Current.CancellationToken);
    var allowed = await allowedResponse.Content.ReadFromJsonAsync<DODtos.DeploymentTagCapabilityResponseDto>(
      TestContext.Current.CancellationToken);

    var deniedResponse = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.DeploymentOptionsEndpoint}/tag-capability?tenantId={tenant.Id}",
      new DODtos.DeploymentTagCapabilityRequestDto(Guid.NewGuid(), null),
      TestContext.Current.CancellationToken);
    var denied = await deniedResponse.Content.ReadFromJsonAsync<DODtos.DeploymentTagCapabilityResponseDto>(
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
    Assert.True(allowed!.Allowed);
    Assert.Equal(HttpStatusCode.OK, deniedResponse.StatusCode);
    Assert.False(denied!.Allowed);
  }

  [Fact]
  public async Task GetTagCapability_TenantWideDeviceTagsWrite_AllowsNewDevice()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(
      tenant.Id,
      email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"tags-{Guid.NewGuid():N}@t.local");

    using (var scope = testServer.Services.CreateScope())
    {
      await using var db = scope.ServiceProvider.GetRequiredService<Data.AppDb>();
      db.PermissionAssignments.AddRange(
        CreateAssignment(user.Id, PermissionNames.AgentInstall, tenant.Id, PermissionScopeKind.Tenant, tenant.Id),
        CreateAssignment(user.Id, PermissionNames.DeviceTagsWrite, tenant.Id, PermissionScopeKind.Tenant, tenant.Id));
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    var response = await httpClient.PostAsJsonAsync(
      $"{HttpConstants.V1.DeploymentOptionsEndpoint}/tag-capability?tenantId={tenant.Id}",
      new DODtos.DeploymentTagCapabilityRequestDto(null, null),
      TestContext.Current.CancellationToken);
    var result = await response.Content.ReadFromJsonAsync<DODtos.DeploymentTagCapabilityResponseDto>(
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.NotNull(result);
    Assert.True(result.Allowed);
  }

  [Fact]
  public async Task GetTagCapability_WhenUserRecordDeleted_ReturnsForbid()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, user) = await scope.CreateControllerWithTestData<DeploymentOptionsController>();

    // A deleted user can keep presenting a previously issued token. The route must refuse to
    // answer capability questions for a principal whose user record no longer exists.
    using (var deleteScope = testApp.Services.CreateScope())
    {
      await using var db = deleteScope.ServiceProvider.GetRequiredService<Data.AppDb>();
      db.Users.Remove(db.Users.Single(x => x.Id == user.Id));
      db.SaveChanges();
    }

    var result = await controller.GetTagCapability(
      tenant.Id,
      new DODtos.DeploymentTagCapabilityRequestDto(null, null),
      scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetTagCapability_WithServerPrincipal_AllowsNewDevice()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var tenant = await scope.ServiceProvider.CreateTestTenant("Server Principal Tenant");

    var controller = await scope.CreateControllerWithServerPrincipal<DeploymentOptionsController>();
    var result = await controller.GetTagCapability(
      tenant.Id,
      new DODtos.DeploymentTagCapabilityRequestDto(null, null),
      scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
      TestContext.Current.CancellationToken);

    var response = Assert.IsType<DODtos.DeploymentTagCapabilityResponseDto>(
      Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.True(response.Allowed);
  }

  [Fact]
  public async Task Get_WithAgentInstallerPreset_ReturnsConfiguredDeploymentSettings()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(
      tenant.Id,
      email: $"seed-{Guid.NewGuid():N}@t.local");
    var installer = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"installer-{Guid.NewGuid():N}@t.local",
      PermissionPresets.AgentInstaller);
    using (var scope = testServer.Services.CreateScope())
    {
      var settingsManager = scope.ServiceProvider.GetRequiredService<Services.Settings.ITenantSettingsManager>();
      var result = await settingsManager.SetSettings(
        tenant.Id,
        new InternalDtos.TenantSettingsDto(true, "deployment-instance", null),
        TestContext.Current.CancellationToken);
      Assert.True(result.IsSuccess);
    }

    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, installer.Id, installer.TenantId, "test"));

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.DeploymentOptionsEndpoint}?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);
    var options = await response.Content.ReadFromJsonAsync<DODtos.DeploymentOptionsDto>(
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.NotNull(options);
    Assert.True(options.AppendInstanceId);
    Assert.Equal("deployment-instance", options.InstanceId);
  }

  [Fact]
  public async Task Get_WithoutAgentInstall_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    await testServer.Services.CreateTestUser(
      tenant.Id,
      email: $"seed-{Guid.NewGuid():N}@t.local");
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"plain-{Guid.NewGuid():N}@t.local");
    using var httpClient = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));

    var response = await httpClient.GetAsync(
      $"{HttpConstants.V1.DeploymentOptionsEndpoint}?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Get_WithServerPrincipal_ReturnsConfiguredDeploymentSettings()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var tenant = await scope.ServiceProvider.CreateTestTenant("Server Principal Tenant");

    using (var settingsScope = testApp.CreateScope())
    {
      var settingsManager = settingsScope.ServiceProvider.GetRequiredService<Services.Settings.ITenantSettingsManager>();
      var result = await settingsManager.SetSettings(
        tenant.Id,
        new InternalDtos.TenantSettingsDto(true, "server-instance", null),
        TestContext.Current.CancellationToken);
      Assert.True(result.IsSuccess);
    }

    var controller = await scope.CreateControllerWithServerPrincipal<DeploymentOptionsController>();
    var response = await controller.Get(tenant.Id, TestContext.Current.CancellationToken);

    var options = Assert.IsType<DODtos.DeploymentOptionsDto>(
      Assert.IsType<OkObjectResult>(response.Result!).Value);
    Assert.True(options.AppendInstanceId);
    Assert.Equal("server-instance", options.InstanceId);
  }

  private static Data.Entities.PermissionAssignment CreateAssignment(
    Guid principalId,
    string permissionName,
    Guid tenantId,
    PermissionScopeKind scopeKind,
    Guid? scopeId)
  {
    return new Data.Entities.PermissionAssignment
    {
      PrincipalKind = PermissionPrincipalKind.User,
      PrincipalId = principalId,
      PermissionName = permissionName,
      Effect = PermissionEffect.Allow,
      ScopeKind = scopeKind,
      ScopeId = scopeId,
      OwningTenantId = tenantId,
      IsEnabled = true
    };
  }

  private static async Task<HttpClient> CreatePatClient(
    TestWebServer testServer,
    PrincipalDescriptor actor)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Deployment Options Test PAT", PersonalAccessTokenPermissionMode.InheritOwner),
      actor.PrincipalId,
      actor);
    Assert.True(patResult.IsSuccess);

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }
}