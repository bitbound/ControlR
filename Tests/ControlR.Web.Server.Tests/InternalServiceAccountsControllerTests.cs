using ControlR.Web.Server.Api.Internal;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class InternalServiceAccountsControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AddCredentialForTenant_DisabledAccount_Returns403()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-disabled-test@t.local",
      presets: PermissionPresets.TenantAdministrator);

    var account = await CreateTenantAccount(testApp, tenant.Id, "Disabled Tenant SA");
    await using (var appDb = scope.ServiceProvider.GetRequiredService<AppDb>())
    {
      var entity = await appDb.ServiceAccounts.FirstAsync(x => x.Id == account.Id, TestContext.Current.CancellationToken);
      entity.IsEnabled = false;
      await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    var result = await controller.AddCredential(
      account.Id,
      new InternalDtos.CreateTenantServiceAccountCredentialRequestDto("New Credential", null),
      TestContext.Current.CancellationToken);

    var forbidden = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(403, forbidden.StatusCode);
  }

  [Fact]
  public async Task Create_WithTenantAdmin_ReturnsCreatedAccount()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, _, _) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-create-test@t.local",
      presets: PermissionPresets.TenantAdministrator);

    var result = await controller.Create(
      new InternalDtos.CreateTenantServiceAccountRequestDto("New Tenant SA", "desc"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<InternalDtos.TenantServiceAccountDto>(ok.Value);
    Assert.Equal("New Tenant SA", dto.Name);
    Assert.Equal("desc", dto.Description);
    Assert.True(dto.IsEnabled);
    Assert.Empty(dto.Credentials);
  }

  [Fact]
  public async Task Delete_FromOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, _, _) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-delete-a@t.local",
      presets: PermissionPresets.TenantAdministrator);

    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var foreignAccount = await CreateTenantAccount(testApp, tenantB.Id, "Foreign Tenant SA");

    var result = await controller.Delete(foreignAccount.Id, TestContext.Current.CancellationToken);

    var notFound = Assert.IsType<ObjectResult>(result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public async Task GetAll_WithTenantAdmin_ReturnsOnlyCallersTenantAccounts()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-getall-a@t.local",
      presets: PermissionPresets.TenantAdministrator);

    await CreateTenantAccount(testApp, tenantA.Id, "Tenant A SA");

    // Account in a different tenant must not be visible.
    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    await CreateTenantAccount(testApp, tenantB.Id, "Tenant B SA");

    var result = await controller.GetAll(TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var accounts = Assert.IsType<List<InternalDtos.TenantServiceAccountDto>>(ok.Value);
    var names = accounts.Select(a => a.Name).ToArray();
    Assert.Contains("Tenant A SA", names);
    Assert.DoesNotContain("Tenant B SA", names);
  }

  [Fact]
  public async Task Get_FromOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenantA, _) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-get-a@t.local",
      presets: PermissionPresets.TenantAdministrator);

    // Account owned by a different tenant.
    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var foreignAccount = await CreateTenantAccount(testApp, tenantB.Id, "Foreign Tenant SA");

    var result = await controller.Get(foreignAccount.Id, TestContext.Current.CancellationToken);

    var notFound = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public async Task RevokeCredential_ForOwnTenantAccount_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, tenant, _) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-revoke@t.local",
      presets: PermissionPresets.TenantAdministrator);

    var account = await CreateTenantAccount(testApp, tenant.Id, "Revocable Tenant SA");

    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var credResult = await manager.AddCredentialForTenant(
      account.Id, tenant.Id, "Cred", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var result = await controller.RevokeCredential(
      account.Id, credResult.Value.Credential.Id, TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);
  }

  [Fact]
  public async Task Update_FromOtherTenant_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var (controller, _, _) = await scope.CreateControllerWithTestData<TenantServiceAccountsController>(
      userEmail: "tenant-update-a@t.local",
      presets: PermissionPresets.TenantAdministrator);

    var tenantB = await testApp.Services.CreateTestTenant("Tenant B");
    var foreignAccount = await CreateTenantAccount(testApp, tenantB.Id, "Foreign Tenant SA");

    var result = await controller.Update(
      foreignAccount.Id,
      new InternalDtos.UpdateTenantServiceAccountRequestDto("Renamed", null, true),
      TestContext.Current.CancellationToken);

    var notFound = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }

  private static async Task<ServiceAccountResult> CreateTenantAccount(TestApp testApp, Guid tenantId, string name)
  {
    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var result = await manager.CreateForTenant(
      name, null, tenantId, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess, $"CreateForTenant failed: {result.Reason}");
    return result.Value;
  }
}
