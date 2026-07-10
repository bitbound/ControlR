using ControlR.Web.Server.Api.V0;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V0;

public class AddCredentialResponseCodeTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task AddCredential_DisabledAccount_Returns409()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    var accountId = Guid.Empty;

    using (var scope = testApp.CreateScope())
    {
      var services = scope.ServiceProvider;
      var manager = services.GetRequiredService<IServiceAccountManager>();

      var saResult = await manager.CreateServer("Disabled SA", null, TestContext.Current.CancellationToken);
      Assert.True(saResult.IsSuccess);
      accountId = saResult.Value.ServiceAccount.Id;

      var appDb = services.GetRequiredService<AppDb>();
      var account = await appDb.ServiceAccounts.FindAsync([accountId], TestContext.Current.CancellationToken);
      Assert.NotNull(account);
      account.IsEnabled = false;
      await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    using (var scope = testApp.CreateScope())
    {
      var services = scope.ServiceProvider;

      var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
        ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

      var result = await controller.AddCredential(
        accountId,
        new CreateServiceAccountCredentialRequestDto("New Credential"),
        TestContext.Current.CancellationToken);

      var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
      Assert.Equal(409, conflict.StatusCode);
    }
  }

  [Fact]
  public async Task AddCredential_EmptyName_Returns400()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateServer("Empty Name SA", null, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.ServiceAccount.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.AddCredential(
      accountId,
      new CreateServiceAccountCredentialRequestDto("   "),
      TestContext.Current.CancellationToken);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
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

    var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
    Assert.Equal(404, notFound.StatusCode);
  }

  [Fact]
  public async Task AddCredential_Succeeds_Returns200()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var saResult = await manager.CreateServer("Success SA", null, TestContext.Current.CancellationToken);
    Assert.True(saResult.IsSuccess);
    var accountId = saResult.Value.ServiceAccount.Id;

    var controller = await TestPrincipalHelper.CreateControllerWithServerServiceAccountAsync<
      ServiceAccountsController>(scope, cancellationToken: TestContext.Current.CancellationToken);

    var result = await controller.AddCredential(
      accountId,
      new CreateServiceAccountCredentialRequestDto("Secondary Credential"),
      TestContext.Current.CancellationToken);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<CreateServiceAccountCredentialResponseDto>(okResult.Value);
    Assert.NotNull(dto);
    Assert.NotEmpty(dto.PlainTextSecretKey);
  }
}