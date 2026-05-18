using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class AuthControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task ForgotPassword_ReturnsOk_WhenEmailSendingIsDisabled()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<AuthController>();

    var result = await controller.ForgotPassword(
      services.GetRequiredService<IPasswordManager>(),
      new ForgotPasswordRequestDto("missing@example.com"));

    Assert.IsType<OkResult>(result);
  }

  [Fact]
  public async Task ResetPassword_ChangesPassword_AndClearsRequirePasswordChange()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);

    using var scope = testApp.CreateScope();
    var services = scope.ServiceProvider;
    var controller = scope.CreateController<AuthController>();
    var tenant = await services.CreateTestTenant();
    var user = await services.CreateTestUser(tenant.Id, "reset-user@t.local");
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    user = await userManager.FindByIdAsync(user.Id.ToString()) ?? throw new InvalidOperationException("User not found.");

    user.RequirePasswordChange = true;
    await userManager.UpdateAsync(user);

    var resetCode = await userManager.GeneratePasswordResetTokenAsync(user);
    var request = new ResetPasswordRequestDto(user.Email!, resetCode, "N3wP@ssw0rd!");

    var result = await controller.ResetPassword(
      services.GetRequiredService<IPasswordManager>(),
      request);

    Assert.IsType<OkResult>(result);

    using var verificationScope = testApp.CreateScope();
    var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var refreshedUser = await verificationUserManager.FindByIdAsync(user.Id.ToString());
    Assert.NotNull(refreshedUser);
    Assert.False(refreshedUser.RequirePasswordChange);
    Assert.True(await verificationUserManager.CheckPasswordAsync(refreshedUser, request.NewPassword));
  }
}