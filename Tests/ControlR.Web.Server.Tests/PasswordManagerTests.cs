using System.Text;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class PasswordManagerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task ForgotPassword_FailsForKnownUser_ButNotForUnknownUser_WhenNoTrustworthyOrigin()
  {
    // Email sending is on but there is no origin to build the reset link from, so the send has to fail
    // rather than mail a link with a request-derived origin. The unknown address still reports success,
    // which is what keeps the failure from revealing whether an address exists.
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutputHelper,
      extraConfiguration: new Dictionary<string, string?>
      {
        ["AppOptions:DisableEmailSending"] = "false",
        ["AppOptions:PublicBaseUrl"] = "",
      });

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var passwordManager = services.GetRequiredService<IPasswordManager>();
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, "no-origin@t.local");

    var knownUserResult = await passwordManager.ForgotPassword(
      new InternalDtos.ForgotPasswordRequestDto(user.Email!));
    var unknownUserResult = await passwordManager.ForgotPassword(
      new InternalDtos.ForgotPasswordRequestDto("missing@t.local"));

    Assert.False(knownUserResult.IsSuccess);
    Assert.True(unknownUserResult.IsSuccess);
  }

  [Fact]
  public async Task ForgotPassword_ReturnsOk_WhenEmailSendingIsDisabled()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var passwordManager = services.GetRequiredService<IPasswordManager>();

    var result = await passwordManager.ForgotPassword(
      new InternalDtos.ForgotPasswordRequestDto("missing@example.com"));

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task ResetPassword_ChangesPassword_AndClearsRequirePasswordChange()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var passwordManager = services.GetRequiredService<IPasswordManager>();
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, "reset-user@t.local");
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    user = await userManager.FindByIdAsync(user.Id.ToString()) ?? throw new InvalidOperationException("User not found.");

    user.RequirePasswordChange = true;
    await userManager.UpdateAsync(user);

    var resetCode = await userManager.GeneratePasswordResetTokenAsync(user);
    var request = new InternalDtos.ResetPasswordRequestDto(user.Email!, resetCode, "N3wP@ssw0rd!");

    var result = await passwordManager.CompletePasswordReset(request);

    Assert.True(result.IsSuccess, result.Reason);

    using var verificationScope = testApp.CreateScope();
    var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var refreshedUser = await verificationUserManager.FindByIdAsync(user.Id.ToString());
    Assert.NotNull(refreshedUser);
    Assert.False(refreshedUser.RequirePasswordChange);
    Assert.True(await verificationUserManager.CheckPasswordAsync(refreshedUser, request.NewPassword));
  }

  [Fact]
  public async Task ResetPassword_Succeeds_WhenEncodedTokenFromForgotPasswordLink()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var passwordManager = services.GetRequiredService<IPasswordManager>();
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, "encoded-reset@t.local");
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    user = await userManager.FindByIdAsync(user.Id.ToString()) ?? throw new InvalidOperationException("User not found.");
    Assert.NotNull(user.Email);

    var rawToken = await userManager.GeneratePasswordResetTokenAsync(user);
    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

    var request = new InternalDtos.ResetPasswordRequestDto(user.Email, encodedToken, "N3wP@ssw0rd!");

    var result = await passwordManager.CompletePasswordReset(request);

    Assert.True(result.IsSuccess, result.Reason);
  }
}
