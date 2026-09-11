using System.Reflection;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

public class ServerServiceAccountsControllerTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task AddCredential_DisabledAccount_Returns403()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    var accountId = Guid.Empty;

    using (var scope = testApp.CreateScope())
    {
      var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
      await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

      var saResult = await manager.CreateForServer("Disabled SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
      Assert.True(saResult.IsSuccess);
      accountId = saResult.Value.Id;

      var account = await appDb.ServiceAccounts
        .FirstOrDefaultAsync(x => x.Id == accountId, TestContext.Current.CancellationToken);
      Assert.NotNull(account);
      account.IsEnabled = false;
      await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testApp.CreateScope())
    {
      var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
        ServerServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

      var result = await controller.AddCredential(
        accountId,
        new CreateServiceAccountCredentialRequestDto("New Credential"),
        TestContext.Current.CancellationToken);

      var forbidden = Assert.IsType<ObjectResult>(result.Result);
      Assert.Equal(403, forbidden.StatusCode);
    }
  }

  [Fact]
  public async Task AddCredential_EmptyName_Returns400()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Add Credential SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.AddCredential(
      accountId,
      new CreateServiceAccountCredentialRequestDto(""),
      TestContext.Current.CancellationToken);

    var badRequest = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(400, badRequest.StatusCode);
  }

  [Fact]
  public async Task AddCredential_MissingAccount_Returns404()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.AddCredential(
      Guid.NewGuid(),
      new CreateServiceAccountCredentialRequestDto("New Credential"),
      TestContext.Current.CancellationToken);

    var notFound = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public async Task Create_PermissionWriteUserCreatesUnrestricted_Succeeds()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var (controller, _, user) = await scope.CreateControllerWithTestData<ServerServiceAccountsController>(
      userEmail: "v1-perm-write@t.local");

    await GrantServerPermissions(scope.ServiceProvider, user.Id,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite,
      PermissionNames.ServerPermissionsWrite);

    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var result = await controller.Create(
      new CreateServerServiceAccountRequestDto("Unrestricted SA", null, ServiceAccountAccessMode.Unrestricted),
      evaluator,
      TestContext.Current.CancellationToken);

    var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<ServerServiceAccountDto>(createdResult.Value);
    Assert.Equal(ServiceAccountAccessMode.Unrestricted, dto.AccessMode);
    Assert.Equal(nameof(ServerServiceAccountsController.Get), createdResult.ActionName);
  }

  [Fact]
  public void Create_RequiresServerServiceAccountsWritePolicy()
  {
    var attribute = typeof(ServerServiceAccountsController)
      .GetMethod(nameof(ServerServiceAccountsController.Create))!
      .GetCustomAttribute<AuthorizeAttribute>();
    Assert.NotNull(attribute);
    Assert.Equal(PolicyNames.RequireServerServiceAccountsWrite, attribute.Policy);
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_OnMissingName()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var result = await controller.Create(
      new CreateServerServiceAccountRequestDto("", null, ServiceAccountAccessMode.Restricted),
      evaluator,
      TestContext.Current.CancellationToken);

    var problem = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(400, problem.StatusCode);
  }

  [Fact]
  public async Task Create_ReturnsCreatedAtActionResult()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var result = await controller.Create(
      new CreateServerServiceAccountRequestDto("New Test Account", "Description", ServiceAccountAccessMode.Restricted),
      evaluator,
      TestContext.Current.CancellationToken);

    var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<ServerServiceAccountDto>(createdResult.Value);
    Assert.Equal("New Test Account", dto.Name);
    Assert.Equal(ServiceAccountAccessMode.Restricted, dto.AccessMode);
    Assert.Equal(nameof(ServerServiceAccountsController.Get), createdResult.ActionName);
    Assert.Equal(dto.Id, (Guid)createdResult.RouteValues!["serviceAccountId"]!);
  }

  [Fact]
  public async Task Create_WriteOnlyUserCreatesUnrestricted_ReturnsForbidden()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    // A write-only user holds the server service-account write policy but lacks the
    // elevated ServerPermissionsWrite required to grant Unrestricted, so the
    // imperative gate inside Create must deny with 403 Forbidden.
    var (controller, _, user) = await scope.CreateControllerWithTestData<ServerServiceAccountsController>(
      userEmail: "v1-write-only@t.local");

    await GrantServerPermissions(scope.ServiceProvider, user.Id,
      PermissionNames.ServerServiceAccountsRead,
      PermissionNames.ServerServiceAccountsWrite);

    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var result = await controller.Create(
      new CreateServerServiceAccountRequestDto("Unrestricted SA", null, ServiceAccountAccessMode.Unrestricted),
      evaluator,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result.Result);
  }

  [Fact]
  public async Task Delete_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer(
      "Delete Me",
      null,
      ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Delete(accountId, TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(result);

    // Verify the account was actually deleted.
    var remaining = await manager.GetAllForServer(TestContext.Current.CancellationToken);
    Assert.DoesNotContain(remaining, a => a.Id == accountId);
  }

  [Fact]
  public async Task Delete_ReturnsNotFound_WhenNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);
    var notFound = Assert.IsType<ObjectResult>(result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public async Task Delete_Self_Returns403()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer(
      "Self Delete Test",
      null,
      ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await manager.AddCredentialForServer(
      saResult.Value.Id,
      "Test Credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);
    var selfAccountId = saResult.Value.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    // Override the principal with the same account that the controller will be trying to delete.
    var controllerPrincipal = TestPrincipalHelper.CreateServerServiceAccountPrincipal(saResult.Value, credResult.Value.Credential);
    controller.ControllerContext.HttpContext.User = controllerPrincipal;

    var result = await controller.Delete(selfAccountId, TestContext.Current.CancellationToken);
    var forbidden = Assert.IsType<ObjectResult>(result);
    Assert.Equal(403, forbidden.StatusCode);

    // Verify the account still exists.
    var remaining = await manager.GetAllForServer(TestContext.Current.CancellationToken);
    Assert.NotNull(remaining.FirstOrDefault(a => a.Id == selfAccountId));
  }

  [Fact]
  public async Task GetAll_ReturnsList()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);
    var manager = services.GetRequiredService<IServiceAccountManager>();

    // Create another account.
    await manager.CreateForServer("Additional Account", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);

    var result = await controller.GetAll(TestContext.Current.CancellationToken);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<ServerServiceAccountsResponseDto>(okResult.Value);
    Assert.True(response.Items.Count >= 2, "Should have at least 2 accounts");
  }

  [Fact]
  public async Task Get_ReturnsAccount()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Get Me", "Get description", ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Get(accountId, TestContext.Current.CancellationToken);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<ServerServiceAccountDto>(okResult.Value);
    Assert.Equal(accountId, dto.Id);
    Assert.Equal("Get Me", dto.Name);
    Assert.Equal("Get description", dto.Description);
  }

  [Fact]
  public async Task Get_ReturnsNotFound_WhenNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Get(Guid.NewGuid(), TestContext.Current.CancellationToken);
    var notFound = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public async Task PurgeCredential_ActiveCredential_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Purge Active SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await manager.AddCredentialForServer(
      saResult.Value.Id, "Live", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.PurgeCredential(
      saResult.Value.Id, credResult.Value.Credential.Id, TestContext.Current.CancellationToken);
    var badRequest = Assert.IsType<ObjectResult>(result);
    Assert.Equal(400, badRequest.StatusCode);
  }

  [Fact]
  public async Task PurgeCredential_RevokedCredential_ReturnsNoContentAndRemovesIt()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Purge Credential SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.Id;

    var credResult = await manager.AddCredentialForServer(
      accountId, "Purge Me", null, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);
    var credentialId = credResult.Value.Credential.Id;

    var revokeResult = await manager.RevokeCredentialForServer(
      accountId, credentialId, TestActors.User(), TestContext.Current.CancellationToken);
    Assert.True(revokeResult.IsSuccess);

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.PurgeCredential(accountId, credentialId, TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(result);

    await using var appDb = services.GetRequiredService<AppDb>();
    var stillExists = await appDb.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .AnyAsync(x => x.Id == credentialId, TestContext.Current.CancellationToken);
    Assert.False(stillExists);
  }

  [Fact]
  public async Task RevokeCredential_Credential_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Revoke Credential SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);

    var credResult = await manager.AddCredentialForServer(
      saResult.Value.Id,
      "Test Credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);
    Assert.True(credResult.IsSuccess);

    var accountId = saResult.Value.Id;
    var credentialId = credResult.Value.Credential.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.RevokeCredential(accountId, credentialId, TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(result);

    // Verify the credential is revoked.
    await using var appDb = services.GetRequiredService<AppDb>();
    var credential = await appDb.ServiceAccountCredentials
      .FirstOrDefaultAsync(x => x.Id == credentialId, TestContext.Current.CancellationToken);
    Assert.NotNull(credential);
    Assert.NotNull(credential.RevokedAt);
  }

  [Fact]
  public async Task RevokeCredential_NonExistentCredential_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServerServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Revoke NonExistent SA", null, ServiceAccountAccessMode.Unrestricted, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.Id;

    var result = await controller.RevokeCredential(accountId, Guid.NewGuid(), TestContext.Current.CancellationToken);
    var notFound = Assert.IsType<ObjectResult>(result);
    Assert.Equal(404, notFound.StatusCode);
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
}