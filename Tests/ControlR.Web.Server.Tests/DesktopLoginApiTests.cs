using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class InteractiveLoginApiTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task InteractiveLogin_WhenIdentityRequiresTwoFactor_ReturnsRequiresTwoFactor()
  {
    var user = new AppUser
    {
      Email = "desktop-2fa@t.local",
      UserName = "desktop-2fa@t.local",
      TwoFactorEnabled = true
    };

    var userStore = new Mock<IUserStore<AppUser>>();
    var userManager = new Mock<UserManager<AppUser>>(
      userStore.Object,
      null!,
      null!,
      null!,
      null!,
      null!,
      null!,
      null!,
      null!);

    userManager
      .Setup(x => x.FindByEmailAsync(user.Email))
      .ReturnsAsync(user);

    var claimsFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
    var signInManager = new Mock<SignInManager<AppUser>>(
      userManager.Object,
      Mock.Of<IHttpContextAccessor>(),
      claimsFactory.Object,
      Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
      Mock.Of<ILogger<SignInManager<AppUser>>>(),
      Mock.Of<IAuthenticationSchemeProvider>(),
      Mock.Of<IUserConfirmation<AppUser>>());

    signInManager
      .Setup(x => x.CheckPasswordSignInAsync(user, "T3stP@ssw0rd!", true))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

    var bearerTokenOptions = new Mock<IOptionsMonitor<BearerTokenOptions>>();
    var controller = new AuthController();

    var result = await controller.InteractiveLogin(
      signInManager.Object,
      userManager.Object,
      bearerTokenOptions.Object,
      TimeProvider.System,
      new LoginRequestDto(user.Email, "T3stP@ssw0rd!"));

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var payload = Assert.IsType<InteractiveLoginResponseDto>(okResult.Value);
    Assert.True(payload.RequiresTwoFactor);
    Assert.Null(payload.Tokens);
  }

  [Fact]
  public async Task InteractiveLogin_WithValidCredentials_ReturnsBearerTokens()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutputHelper);

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, "interactive-login@t.local");

    using (var scope = testServer.Services.CreateScope())
    {
      var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
      var trackedUser = await userManager.FindByIdAsync(user.Id.ToString());
      Assert.NotNull(trackedUser);
      trackedUser.EmailConfirmed = true;
      await userManager.UpdateAsync(trackedUser);
    }

    using var client = testServer.Factory.CreateClient();
    using var response = await client.PostAsJsonAsync(
      "/api/auth/interactive-login",
      new LoginRequestDto(user.Email!, "T3stP@ssw0rd!"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<InteractiveLoginResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(payload);
    Assert.False(payload.RequiresTwoFactor);
    Assert.NotNull(payload.Tokens);
    Assert.Equal("Bearer", payload.Tokens.TokenType);
    Assert.False(string.IsNullOrWhiteSpace(payload.Tokens.AccessToken));
    Assert.False(string.IsNullOrWhiteSpace(payload.Tokens.RefreshToken));
    Assert.True(payload.Tokens.ExpiresIn > 0);
  }
}