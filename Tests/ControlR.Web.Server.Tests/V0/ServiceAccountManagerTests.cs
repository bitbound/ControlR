using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountManagerTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task ServiceAccountLifecycle_CreateRevokeDelete()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(testOutput);
    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var manager = services.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForServer(
      "Lifecycle SA",
      "Created by test",
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var accountId = createResult.Value.ServiceAccount.Id;
    var credentialId = createResult.Value.ServiceAccount.Credentials[0].Id;
    var apiKey = createResult.Value.PlainTextSecretKey;

    var validateResult = await manager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);
    Assert.True(validateResult.IsSuccess);
    Assert.Equal(accountId, validateResult.Value.ServiceAccount.Id);

    var addCredResult = await manager.AddCredential(
      accountId,
      "Secondary key",
      TestContext.Current.CancellationToken);
    Assert.True(addCredResult.IsSuccess);
    var secondApiKey = addCredResult.Value.PlainTextSecretKey;

    await manager.RevokeCredential(accountId, credentialId, TestContext.Current.CancellationToken);

    var shouldFail = await manager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);
    Assert.False(shouldFail.IsSuccess);

    var shouldPass = await manager.ValidateCredential(secondApiKey, TestContext.Current.CancellationToken);
    Assert.True(shouldPass.IsSuccess);

    await manager.Delete(accountId, TestContext.Current.CancellationToken);

    var allAccounts = await manager.GetAllServer(TestContext.Current.CancellationToken);
    Assert.DoesNotContain(allAccounts, a => a.Id == accountId);
  }
}
