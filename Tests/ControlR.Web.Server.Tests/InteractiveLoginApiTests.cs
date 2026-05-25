using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ControlR.Web.Server.Options;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class InteractiveLoginApiTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task InteractiveLogin_WhenFeatureIsDisabled_ReturnsNotFound()
  {
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

    var claimsFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
    var signInManager = new Mock<SignInManager<AppUser>>(
      userManager.Object,
      Mock.Of<IHttpContextAccessor>(),
      claimsFactory.Object,
      Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
      Mock.Of<ILogger<SignInManager<AppUser>>>(),
      Mock.Of<IAuthenticationSchemeProvider>(),
      Mock.Of<IUserConfirmation<AppUser>>());

    var appOptions = new Mock<IOptionsMonitor<AppOptions>>();
    appOptions.SetupGet(x => x.CurrentValue).Returns(new AppOptions { EnableInteractiveBearerLogin = false });

    var bearerTokenOptions = new Mock<IOptionsMonitor<BearerTokenOptions>>();
    var controller = new AuthController();

    var result = await controller.InteractiveLogin(
      signInManager.Object,
      userManager.Object,
      TimeProvider.System,
      appOptions.Object,
      bearerTokenOptions.Object,
      new LoginRequestDto("user@example.com", "T3stP@ssw0rd!"));

    Assert.IsType<NotFoundResult>(result.Result);
  }

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
      .Setup(x => x.CheckPasswordSignInAsync(user, "T3stP@ssw0rd!", false))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

    var bearerTokenOptions = new Mock<IOptionsMonitor<BearerTokenOptions>>();
    var appOptions = new Mock<IOptionsMonitor<AppOptions>>();
    appOptions.SetupGet(x => x.CurrentValue).Returns(new AppOptions { EnableInteractiveBearerLogin = true });
    var controller = new AuthController();

    var result = await controller.InteractiveLogin(
      signInManager.Object,
      userManager.Object,
      TimeProvider.System,
      appOptions.Object,
      bearerTokenOptions.Object,
      new LoginRequestDto(user.Email, "T3stP@ssw0rd!"));

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var payload = Assert.IsType<InteractiveLoginResponseDto>(okResult.Value);
    Assert.True(payload.RequiresTwoFactor);
    Assert.Null(payload.Tokens);
  }

  [Fact]
  public async Task InteractiveLogin_WhenRecoveryCodeIsInvalid_ReturnsUnauthorized()
  {
    var user = new AppUser
    {
      Email = "desktop-recovery-invalid@t.local",
      UserName = "desktop-recovery-invalid@t.local",
      TwoFactorEnabled = true
    };

    var userManager = CreateUserManager(user);
    userManager
      .Setup(x => x.RedeemTwoFactorRecoveryCodeAsync(user, "recoverycode"))
      .ReturnsAsync(IdentityResult.Failed());

    var signInManager = CreateSignInManager(userManager.Object);
    signInManager
      .Setup(x => x.CheckPasswordSignInAsync(user, "T3stP@ssw0rd!", false))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

    var appOptions = new Mock<IOptionsMonitor<AppOptions>>();
    appOptions.SetupGet(x => x.CurrentValue).Returns(new AppOptions { EnableInteractiveBearerLogin = true });
    var controller = new AuthController();

    var result = await controller.InteractiveLogin(
      signInManager.Object,
      userManager.Object,
      TimeProvider.System,
      appOptions.Object,
      CreateBearerTokenOptionsMonitor(),
      new LoginRequestDto(user.Email, "T3stP@ssw0rd!", TwoFactorRecoveryCode: "recovery code"));

    Assert.IsType<UnauthorizedResult>(result.Result);
  }

  [Fact]
  public async Task InteractiveLogin_WhenRecoveryCodeIsValid_ReturnsBearerTokens()
  {
    var user = new AppUser
    {
      Email = "desktop-recovery-valid@t.local",
      UserName = "desktop-recovery-valid@t.local",
      TwoFactorEnabled = true
    };

    var userManager = CreateUserManager(user);
    userManager
      .Setup(x => x.RedeemTwoFactorRecoveryCodeAsync(user, "recoverycode"))
      .ReturnsAsync(IdentityResult.Success);
    userManager
      .Setup(x => x.ResetAccessFailedCountAsync(user))
      .ReturnsAsync(IdentityResult.Success);

    var signInManager = CreateSignInManager(userManager.Object);
    signInManager
      .Setup(x => x.CheckPasswordSignInAsync(user, "T3stP@ssw0rd!", false))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
    signInManager
      .Setup(x => x.CreateUserPrincipalAsync(user))
      .ReturnsAsync(CreatePrincipal(user));

    var appOptions = new Mock<IOptionsMonitor<AppOptions>>();
    appOptions.SetupGet(x => x.CurrentValue).Returns(new AppOptions { EnableInteractiveBearerLogin = true });
    var controller = new AuthController();

    var result = await controller.InteractiveLogin(
      signInManager.Object,
      userManager.Object,
      TimeProvider.System,
      appOptions.Object,
      CreateBearerTokenOptionsMonitor(),
      new LoginRequestDto(user.Email, "T3stP@ssw0rd!", TwoFactorRecoveryCode: "recovery code"));

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var payload = Assert.IsType<InteractiveLoginResponseDto>(okResult.Value);
    Assert.False(payload.RequiresTwoFactor);
    Assert.NotNull(payload.Tokens);
    Assert.False(string.IsNullOrWhiteSpace(payload.Tokens.AccessToken));
    Assert.False(string.IsNullOrWhiteSpace(payload.Tokens.RefreshToken));
  }

  [Fact]
  public async Task InteractiveLogin_WhenTwoFactorCodeIsInvalid_ReturnsUnauthorized()
  {
    var user = new AppUser
    {
      Email = "desktop-2fa-invalid@t.local",
      UserName = "desktop-2fa-invalid@t.local",
      TwoFactorEnabled = true
    };

    var userManager = CreateUserManager(user);
    userManager
      .Setup(x => x.VerifyTwoFactorTokenAsync(user, userManager.Object.Options.Tokens.AuthenticatorTokenProvider, "123456"))
      .ReturnsAsync(false);

    var signInManager = CreateSignInManager(userManager.Object);
    signInManager
      .Setup(x => x.CheckPasswordSignInAsync(user, "T3stP@ssw0rd!", false))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

    var appOptions = new Mock<IOptionsMonitor<AppOptions>>();
    appOptions.SetupGet(x => x.CurrentValue).Returns(new AppOptions { EnableInteractiveBearerLogin = true });
    var controller = new AuthController();

    var result = await controller.InteractiveLogin(
      signInManager.Object,
      userManager.Object,
      TimeProvider.System,
      appOptions.Object,
      CreateBearerTokenOptionsMonitor(),
      new LoginRequestDto(user.Email, "T3stP@ssw0rd!", TwoFactorCode: "123-456"));

    Assert.IsType<UnauthorizedResult>(result.Result);
  }

  [Fact]
  public async Task InteractiveLogin_WhenTwoFactorCodeIsValid_ReturnsBearerTokens()
  {
    var user = new AppUser
    {
      Email = "desktop-2fa-valid@t.local",
      UserName = "desktop-2fa-valid@t.local",
      TwoFactorEnabled = true
    };

    var userManager = CreateUserManager(user);
    userManager
      .Setup(x => x.VerifyTwoFactorTokenAsync(user, userManager.Object.Options.Tokens.AuthenticatorTokenProvider, "123456"))
      .ReturnsAsync(true);
    userManager
      .Setup(x => x.ResetAccessFailedCountAsync(user))
      .ReturnsAsync(IdentityResult.Success);

    var signInManager = CreateSignInManager(userManager.Object);
    signInManager
      .Setup(x => x.CheckPasswordSignInAsync(user, "T3stP@ssw0rd!", false))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
    signInManager
      .Setup(x => x.CreateUserPrincipalAsync(user))
      .ReturnsAsync(CreatePrincipal(user));

    var appOptions = new Mock<IOptionsMonitor<AppOptions>>();
    appOptions.SetupGet(x => x.CurrentValue).Returns(new AppOptions { EnableInteractiveBearerLogin = true });
    var controller = new AuthController();

    var result = await controller.InteractiveLogin(
      signInManager.Object,
      userManager.Object,
      TimeProvider.System,
      appOptions.Object,
      CreateBearerTokenOptionsMonitor(),
      new LoginRequestDto(user.Email, "T3stP@ssw0rd!", TwoFactorCode: "123-456"));

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var payload = Assert.IsType<InteractiveLoginResponseDto>(okResult.Value);
    Assert.False(payload.RequiresTwoFactor);
    Assert.NotNull(payload.Tokens);
    Assert.False(string.IsNullOrWhiteSpace(payload.Tokens.AccessToken));
    Assert.False(string.IsNullOrWhiteSpace(payload.Tokens.RefreshToken));
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
    Assert.True(payload.Tokens.ExpiresInSeconds > 0);
  }

  private static IOptionsMonitor<BearerTokenOptions> CreateBearerTokenOptionsMonitor()
  {
    var dataProtectionProvider = new EphemeralDataProtectionProvider();
    var options = new BearerTokenOptions
    {
      BearerTokenExpiration = TimeSpan.FromMinutes(5),
      RefreshTokenExpiration = TimeSpan.FromDays(7),
      BearerTokenProtector = new TicketDataFormat(dataProtectionProvider.CreateProtector("interactive-bearer-token")),
      RefreshTokenProtector = new TicketDataFormat(dataProtectionProvider.CreateProtector("interactive-refresh-token"))
    };

    var monitor = new Mock<IOptionsMonitor<BearerTokenOptions>>();
    monitor.Setup(x => x.Get(IdentityConstants.BearerScheme)).Returns(options);
    return monitor.Object;
  }

  private static ClaimsPrincipal CreatePrincipal(AppUser user)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(
      [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
      IdentityConstants.BearerScheme));
  }

  private static Mock<SignInManager<AppUser>> CreateSignInManager(UserManager<AppUser> userManager)
  {
    var claimsFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
    return new Mock<SignInManager<AppUser>>(
      userManager,
      Mock.Of<IHttpContextAccessor>(),
      claimsFactory.Object,
      Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
      Mock.Of<ILogger<SignInManager<AppUser>>>(),
      Mock.Of<IAuthenticationSchemeProvider>(),
      Mock.Of<IUserConfirmation<AppUser>>());
  }

  private static Mock<UserManager<AppUser>> CreateUserManager(AppUser user)
  {
    var email = user.Email ?? throw new InvalidOperationException("Test user email is required.");
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
      .Setup(x => x.FindByEmailAsync(email))
      .ReturnsAsync(user);

    return userManager;
  }
}