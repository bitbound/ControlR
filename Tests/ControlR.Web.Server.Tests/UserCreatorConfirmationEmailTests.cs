using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins that a caller learns when a confirmation email cannot be delivered.
/// </summary>
/// <remarks>
/// <see cref="IUserCreator"/> only needs a confirmation link for a brand-new tenant that is not being
/// started by the first user, so the test creates one user first to leave that path.
/// Ref: https://github.com/bitbound/ControlR/issues/175
/// </remarks>
public class UserCreatorConfirmationEmailTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task CreateUser_Fails_WhenNoTrustworthyOriginCanBackTheConfirmationLink()
  {
    // No AppOptions:PublicBaseUrl and AllowedHosts still "*", so there is no origin the server may put
    // in front of a user. Before this change the account was created and reported back as a success,
    // leaving every one of UserCreator's six callers believing a usable account existed.
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      extraConfiguration: new Dictionary<string, string?>
      {
        ["AppOptions:DisableEmailSending"] = "false",
      });

    var seedTenant = await testApp.Services.CreateTestTenant();
    await testApp.Services.CreateTestUser(seedTenant.Id, "existing@t.local");

    using var scope = testApp.Services.CreateScope();
    var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

    var result = await userCreator.CreateUser("orphan@t.local", "T3stP@ssw0rd!", returnUrl: null,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(result.Succeeded);
    Assert.Contains(
      result.IdentityResult.Errors,
      error => error.Code == UserCreator.ConfirmationEmailUnavailableErrorCode);
  }
}
