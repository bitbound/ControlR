using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins that a caller learns when a confirmation email cannot be delivered.
/// </summary>
/// <remarks>
/// <see cref="IUserCreator"/> only needs a confirmation link for a brand-new tenant that is not being
/// started by the first user, so the test creates one user first to leave that path.
/// </remarks>
public class UserCreatorConfirmationEmailTests(ITestOutputHelper testOutput)
{
  [Fact]
  public async Task CreateUser_Fails_WhenNoTrustworthyOriginCanBackTheConfirmationLink()
  {
    // No AppOptions:PublicBaseUrl, and the configured URL is the only origin source, so the
    // confirmation link cannot be built. The failure is returned before the account is created.
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      extraConfiguration: new Dictionary<string, string?>
      {
        ["AppOptions:DisableEmailSending"] = "false",
        ["AppOptions:PublicBaseUrl"] = "",
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

    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var userExists = await db.Users.AnyAsync(
      x => x.Email == "orphan@t.local",
      TestContext.Current.CancellationToken);
      
    Assert.False(userExists);
  }

  [Fact]
  public async Task CreateUser_WithConfiguredOrigin_SendsConfirmationLinkToConfiguredHost()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      extraConfiguration: new Dictionary<string, string?>
      {
        ["AppOptions:DisableEmailSending"] = "false",
        ["AppOptions:PublicBaseUrl"] = "https://controlr.test",
      });

    var seedTenant = await testApp.Services.CreateTestTenant();
    await testApp.Services.CreateTestUser(seedTenant.Id, "existing@t.local");

    using var scope = testApp.Services.CreateScope();
    var sender = new Mock<IEmailSender<AppUser>>();
    var userCreator = ActivatorUtilities.CreateInstance<UserCreator>(scope.ServiceProvider, sender.Object);

    var result = await userCreator.CreateUser("new@t.local", "T3stP@ssw0rd!", returnUrl: null,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(result.Succeeded);
    sender.Verify(email => email.SendConfirmationLinkAsync(
      It.IsAny<AppUser>(), "new@t.local",
      It.Is<string>(link => link.StartsWith("https://controlr.test/Account/ConfirmEmail?", StringComparison.Ordinal) &&
        !link.Contains("untrusted.t.local", StringComparison.Ordinal))), Times.Once);
  }
}
