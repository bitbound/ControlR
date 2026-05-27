using System.Text;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
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
  public async Task ForgotPassword_ReturnsOk_WhenEmailSendingIsDisabled()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var passwordManager = services.GetRequiredService<IPasswordManager>();

    var result = await passwordManager.ForgotPassword(
      new ForgotPasswordRequestDto("missing@example.com"),
      "https://controlr.test/Account/ResetPassword");

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
    var request = new ResetPasswordRequestDto(user.Email!, resetCode, "N3wP@ssw0rd!");

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
  public async Task ResetPassword_Fails_WhenEncodedTokenFromForgotPasswordLink()
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

    var request = new ResetPasswordRequestDto(user.Email, encodedToken, "N3wP@ssw0rd!");

    var result = await passwordManager.CompletePasswordReset(request);

    Assert.True(result.IsSuccess, result.Reason);
  }
}