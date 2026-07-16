using System.Reflection;
using ControlR.Web.Server.Api.V0;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountsControllerTests(ITestOutputHelper testOutput)
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

      var saResult = await manager.CreateForServer("Disabled SA", null, TestContext.Current.CancellationToken);
      Assert.True(saResult.IsSuccess);
      accountId = saResult.Value.ServiceAccount.Id;

      var account = await appDb.ServiceAccounts
        .FirstOrDefaultAsync(x => x.Id == accountId, TestContext.Current.CancellationToken);
      Assert.NotNull(account);
      account.IsEnabled = false;
      await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testApp.CreateScope())
    {
      var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
        ServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

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

    var saResult = await manager.CreateForServer("Add Credential SA", null, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.ServiceAccount.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

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
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.AddCredential(
      Guid.NewGuid(),
      new CreateServiceAccountCredentialRequestDto("New Credential"),
      TestContext.Current.CancellationToken);

    var notFound = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public void Create_RequiresServerServiceAccountPolicy()
  {
    var attribute = typeof(ServiceAccountsController)
      .GetCustomAttribute<AuthorizeAttribute>();
    Assert.NotNull(attribute);
    Assert.Equal(RequireServerServiceAccountPolicy.PolicyName, attribute.Policy);
  }

  [Fact]
  public async Task Create_ReturnsBadRequest_OnMissingName()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Create(
      new CreateServiceAccountRequestDto("", null),
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
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Create(
      new CreateServiceAccountRequestDto("New Test Account", "Description"),
      TestContext.Current.CancellationToken);

    var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
    var dto = Assert.IsType<CreateServiceAccountResponseDto>(createdResult.Value);
    Assert.Equal("New Test Account", dto.ServiceAccount.Name);
    Assert.NotNull(dto.PlainTextSecretKey);
    Assert.Equal(nameof(ServiceAccountsController.Get), createdResult.ActionName);
    Assert.Equal(dto.ServiceAccount.Id, (Guid)createdResult.RouteValues!["serviceAccountId"]!);
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
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.ServiceAccount.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Delete(accountId, TestContext.Current.CancellationToken);
    Assert.IsType<NoContentResult>(result);

    // Verify the account was actually deleted.
    var remaining = await manager.GetAllServer(TestContext.Current.CancellationToken);
    Assert.DoesNotContain(remaining, a => a.Id == accountId);
  }

  [Fact]
  public async Task Delete_ReturnsNotFound_WhenNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

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
      TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var selfAccountId = saResult.Value.ServiceAccount.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    // Override the principal with the same account that the controller will be trying to delete.
    var controllerPrincipal = TestPrincipalHelper.CreateServerServiceAccountPrincipal(saResult.Value);
    controller.ControllerContext.HttpContext.User = controllerPrincipal;

    var result = await controller.Delete(selfAccountId, TestContext.Current.CancellationToken);
    var forbidden = Assert.IsType<ObjectResult>(result);
    Assert.Equal(403, forbidden.StatusCode);

    // Verify the account still exists.
    var remaining = await manager.GetAllServer(TestContext.Current.CancellationToken);
    Assert.NotNull(remaining.FirstOrDefault(a => a.Id == selfAccountId));
  }

  [Fact]
  public async Task GetAll_ReturnsList()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);
    var manager = services.GetRequiredService<IServiceAccountManager>();

    // Create another account.
    await manager.CreateForServer("Additional Account", null, TestContext.Current.CancellationToken);

    var result = await controller.GetAll(TestContext.Current.CancellationToken);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var list = Assert.IsType<List<ServiceAccountDto>>(okResult.Value);
    Assert.True(list.Count >= 2, "Should have at least 2 accounts");
  }

  [Fact]
  public async Task Get_ReturnsAccount()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Get Me", "Get description", TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.ServiceAccount.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Get(accountId, TestContext.Current.CancellationToken);
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<ServiceAccountDto>(okResult.Value);
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
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.Get(Guid.NewGuid(), TestContext.Current.CancellationToken);
    var notFound = Assert.IsType<ObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public async Task RevokeCredential_Credential_ReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Revoke Credential SA", null, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.ServiceAccount.Id;
    var credentialId = saResult.Value.ServiceAccount.Credentials[0].Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, accountName: "Controller SA", cancellationToken: TestContext.Current.CancellationToken);

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
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateForServer("Revoke NonExistent SA", null, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.ServiceAccount.Id;

    var result = await controller.RevokeCredential(accountId, Guid.NewGuid(), TestContext.Current.CancellationToken);
    var notFound = Assert.IsType<ObjectResult>(result);
    Assert.Equal(404, notFound.StatusCode);
  }
}