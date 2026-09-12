using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Tenant isolation and creator-ownership behavior of the V1 installer-keys endpoints.
/// Direct controller calls bypass endpoint policies (authorization middleware), so these
/// exercise the in-handler tenant resolution, the manager ownership checks, and the
/// collapse of creator-mismatch failures into 404.
/// </summary>
public class InstallerKeysV1ControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Delete_WhenKeyWasCreatedByAnotherUser_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (ownerController, tenant, _) = await scope.CreateControllerWithTestData<InstallerKeysController>(
      "Shared Tenant",
      "keyowner@test.local");
    var keyId = await CreateKeyAsync(ownerController, tenant.Id);

    var userB = await services.CreateTestUser(tenant.Id, "deleter@test.local");
    var controllerB = await scope.CreateControllerWithUser<InstallerKeysController>(userB);

    var result = await controllerB.Delete(keyId, tenant.Id, TestContext.Current.CancellationToken);

    // Creator mismatch must be indistinguishable from a nonexistent key.
    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task Delete_WithServerPrincipal_RemovesAnotherUsersKey()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (ownerController, tenant, _) = await scope.CreateControllerWithTestData<InstallerKeysController>(
      "Shared Tenant",
      "sakeyowner@test.local");
    var keyId = await CreateKeyAsync(ownerController, tenant.Id);

    var serverController = await scope.CreateControllerWithServerPrincipal<InstallerKeysController>();
    var deleteResult = await serverController.Delete(keyId, tenant.Id, TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(deleteResult);

    var listResult = await serverController.GetAll(tenant.Id, TestContext.Current.CancellationToken);
    var response = Assert.IsType<InstallerKeysResponseDto>(Assert.IsType<OkObjectResult>(listResult.Result!).Value);
    Assert.Empty(response.Items);
  }

  [Fact]
  public async Task GetAll_WhenCallerAsksForAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();

    var (controllerA, tenantA, _) = await scope.CreateControllerWithTestData<InstallerKeysController>(
      "Tenant A",
      "a@test.local");
    var tenantB = await scope.ServiceProvider.CreateTestTenant("Tenant B");

    var result = await controllerA.GetAll(tenantB.Id, TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task GetAll_WhenCallerLacksManageAll_OnlyReturnsOwnKeys()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (controllerA, tenant, _) = await scope.CreateControllerWithTestData<InstallerKeysController>(
      "Shared Tenant",
      "owner@test.local");
    var ownKey = await CreateKeyAsync(controllerA, tenant.Id);

    var userB = await services.CreateTestUser(tenant.Id, "other@test.local");
    var controllerB = await scope.CreateControllerWithUser<InstallerKeysController>(userB);
    await CreateKeyAsync(controllerB, tenant.Id);

    var result = await controllerA.GetAll(tenant.Id, TestContext.Current.CancellationToken);

    var response = Assert.IsType<InstallerKeysResponseDto>(Assert.IsType<OkObjectResult>(result.Result!).Value);
    Assert.Single(response.Items);
    Assert.Equal(ownKey, response.Items[0].Id);
  }

  [Fact]
  public async Task GetAll_WithServerPrincipal_ReturnsRequestedTenantKeys()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (creatorController, tenant, _) = await scope.CreateControllerWithTestData<InstallerKeysController>(
      "Key Tenant",
      "creator@test.local");
    var createResult = await creatorController.Create(new CreateInstallerKeyRequestDto(
      tenant.Id,
      InstallerKeyType.Persistent));
    var keyId = Assert.IsType<V1Dtos.CreateInstallerKeyResponseDto>(
      Assert.IsType<OkObjectResult>(createResult.Result!).Value).Id;

    var serverController = await scope.CreateControllerWithServerPrincipal<InstallerKeysController>();
    var listResult = await serverController.GetAll(tenant.Id, TestContext.Current.CancellationToken);

    var response = Assert.IsType<InstallerKeysResponseDto>(Assert.IsType<OkObjectResult>(listResult.Result!).Value);
    Assert.Single(response.Items);
    Assert.Equal(keyId, response.Items[0].Id);
  }

  [Fact]
  public async Task GetUsages_WhenKeyWasCreatedByAnotherUser_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (ownerController, tenant, _) = await scope.CreateControllerWithTestData<InstallerKeysController>(
      "Shared Tenant",
      "usageowner@test.local");
    var keyId = await CreateKeyAsync(ownerController, tenant.Id);

    var userB = await services.CreateTestUser(tenant.Id, "usagereader@test.local");
    var controllerB = await scope.CreateControllerWithUser<InstallerKeysController>(userB);

    var result = await controllerB.GetUsages(keyId, tenant.Id, TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task Rename_WhenKeyWasCreatedByAnotherUser_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var (ownerController, tenant, _) = await scope.CreateControllerWithTestData<InstallerKeysController>(
      "Shared Tenant",
      "renameowner@test.local");
    var keyId = await CreateKeyAsync(ownerController, tenant.Id);

    var userB = await services.CreateTestUser(tenant.Id, "renamer@test.local");
    var controllerB = await scope.CreateControllerWithUser<InstallerKeysController>(userB);

    var result = await controllerB.Rename(
      keyId,
      tenant.Id,
      new RenameInstallerKeyRequestDto("stolen name"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
  }

  private static async Task<Guid> CreateKeyAsync(InstallerKeysController controller, Guid tenantId)
  {
    var result = await controller.Create(new CreateInstallerKeyRequestDto(
      tenantId,
      InstallerKeyType.Persistent));

    return Assert.IsType<V1Dtos.CreateInstallerKeyResponseDto>(
      Assert.IsType<OkObjectResult>(result.Result!).Value).Id;
  }
}
