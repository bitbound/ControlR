using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
namespace ControlR.Web.Server.Api;

[Route(HttpConstants.AuthEndpoint)]
[ApiController]
[Authorize]
public class AuthController : ControllerBase
{
  [Authorize]
  [HttpPost("change-password")]
  public async Task<IActionResult> ChangePassword(
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] IPasswordManager passwordManager,
    [FromBody] ChangePasswordRequestDto request)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return BadRequest("User not found");
    }

    var result = await passwordManager.ChangePassword(user, request);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }

  [AllowAnonymous]
  [HttpPost("interactive-login")]
  public async Task<ActionResult<InteractiveLoginResponseDto>> InteractiveLogin(
    [FromServices] SignInManager<AppUser> signInManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
    [FromServices] TimeProvider timeProvider,
    [FromBody] LoginRequestDto request)
  {
    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
      return Unauthorized();
    }

    var result = await signInManager.CheckPasswordSignInAsync(
      user,
      request.Password,
      lockoutOnFailure: true);

    if (result.RequiresTwoFactor &&
        string.IsNullOrWhiteSpace(request.TwoFactorCode) &&
        string.IsNullOrWhiteSpace(request.TwoFactorRecoveryCode))
    {
      return Ok(new InteractiveLoginResponseDto(RequiresTwoFactor: true));
    }

    if (result.RequiresTwoFactor)
    {
      if (!string.IsNullOrWhiteSpace(request.TwoFactorRecoveryCode))
      {
        var recoveryCodeResult = await userManager.RedeemTwoFactorRecoveryCodeAsync(
          user,
          request.TwoFactorRecoveryCode.Replace(" ", string.Empty));

        result = recoveryCodeResult.Succeeded
          ? Microsoft.AspNetCore.Identity.SignInResult.Success
          : Microsoft.AspNetCore.Identity.SignInResult.Failed;
      }
      else if (!string.IsNullOrWhiteSpace(request.TwoFactorCode))
      {
        var normalizedCode = request.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
          user,
          userManager.Options.Tokens.AuthenticatorTokenProvider,
          normalizedCode);

        result = isValid
          ? Microsoft.AspNetCore.Identity.SignInResult.Success
          : Microsoft.AspNetCore.Identity.SignInResult.Failed;
      }

      if (result.Succeeded)
      {
        await userManager.ResetAccessFailedCountAsync(user);
      }
    }

    if (!result.Succeeded)
    {
      return Unauthorized();
    }

    var principal = await signInManager.CreateUserPrincipalAsync(user);
    var tokens = CreateInteractiveLoginTokens(
      principal,
      bearerTokenOptions.Get(IdentityConstants.BearerScheme),
      timeProvider);

    return Ok(new InteractiveLoginResponseDto(RequiresTwoFactor: false, Tokens: tokens));
  }

  [Authorize]
  [HttpPost("logout")]
  public async Task<IActionResult> Logout(
    [FromServices] SignInManager<AppUser> signInManager)
  {
    await signInManager.SignOutAsync();
    return NoContent();
  }

  private static AccessTokenResponseDto CreateInteractiveLoginTokens(
    ClaimsPrincipal principal,
    BearerTokenOptions options,
    TimeProvider timeProvider)
  {
    var utcNow = timeProvider.GetUtcNow();
    var bearerTicket = new AuthenticationTicket(
      principal,
      new AuthenticationProperties
      {
        ExpiresUtc = utcNow + options.BearerTokenExpiration
      },
      $"{IdentityConstants.BearerScheme}:AccessToken");

    var refreshTicket = new AuthenticationTicket(
      principal,
      new AuthenticationProperties
      {
        ExpiresUtc = utcNow + options.RefreshTokenExpiration
      },
      $"{IdentityConstants.BearerScheme}:RefreshToken");

    return new AccessTokenResponseDto(
      TokenType: "Bearer",
      AccessToken: options.BearerTokenProtector.Protect(bearerTicket),
      ExpiresIn: (int)options.BearerTokenExpiration.TotalSeconds,
      RefreshToken: options.RefreshTokenProtector.Protect(refreshTicket));
  }
}
