using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ControlR.Web.Server.Api;

[Route(HttpConstants.AuthEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName("Internal")]
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
      return BadRequest("User not found.");
    }

    var result = await passwordManager.ChangePassword(user, request);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }

  [AllowAnonymous]
  [EnableRateLimiting(AnonymousAuthRateLimitPolicy.PolicyName)]
  [HttpPost("change-password-with-credentials")]
  public async Task<IActionResult> ChangePasswordWithCredentials(
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] SignInManager<AppUser> signInManager,
    [FromServices] IPasswordManager passwordManager,
    [FromBody] CredentialPasswordChangeRequestDto request)
  {
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.CurrentPassword) ||
        string.IsNullOrWhiteSpace(request.NewPassword))
    {
      return BadRequest("Email, current password, and new password are required.");
    }

    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
      return Unauthorized();
    }

    var result = await signInManager.CheckPasswordSignInAsync(
      user,
      request.CurrentPassword,
      lockoutOnFailure: false);

    if (result.IsLockedOut)
    {
      return Unauthorized();
    }

    if (!result.Succeeded)
    {
      await userManager.AccessFailedAsync(user);
      return Unauthorized();
    }

    if (user.TwoFactorEnabled && string.IsNullOrWhiteSpace(request.TwoFactorCode))
    {
      return BadRequest("Two-factor code is required.");
    }

    if (user.TwoFactorEnabled && !string.IsNullOrWhiteSpace(request.TwoFactorCode))
    {
      var normalizedCode = request.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
      var isValid = await userManager.VerifyTwoFactorTokenAsync(
        user,
        userManager.Options.Tokens.AuthenticatorTokenProvider,
        normalizedCode);

      if (!isValid)
      {
        await userManager.AccessFailedAsync(user);
        return BadRequest("Two-factor code is incorrect.");
      }
    }

    await userManager.ResetAccessFailedCountAsync(user);

    var resetResult = await passwordManager.ChangePassword(user, new ChangePasswordRequestDto(
      request.CurrentPassword,
      request.NewPassword));

    if (!resetResult.IsSuccess)
    {
      return BadRequest(resetResult.Reason);
    }

    return Ok();
  }

  [AllowAnonymous]
  [EnableRateLimiting(AnonymousAuthRateLimitPolicy.PolicyName)]
  [HttpPost("complete-password-reset")]
  public async Task<IActionResult> CompletePasswordReset(
    [FromServices] IPasswordManager passwordManager,
    [FromBody] ResetPasswordRequestDto request)
  {
    var result = await passwordManager.CompletePasswordReset(request);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }

  [Authorize]
  [HttpGet("me")]
  public async Task<ActionResult<CurrentUserResponseDto>> GetCurrentUser(
    [FromServices] UserManager<AppUser> userManager)
  {
    var user = await userManager.GetUserAsync(User);
    if (user is null)
    {
      return NotFound();
    }

    ArgumentException.ThrowIfNullOrWhiteSpace(user.UserName);
    ArgumentException.ThrowIfNullOrWhiteSpace(user.Email);

    return Ok(new CurrentUserResponseDto(
      user.Id,
      user.UserName,
      user.Email,
      user.CreatedAt,
      user.IsOnline,
      user.RequirePasswordChange,
      user.TwoFactorEnabled,
      user.EmailConfirmed,
      user.TenantId));
  }

  [AllowAnonymous]
  [EnableRateLimiting(AnonymousAuthRateLimitPolicy.PolicyName)]
  [HttpPost("interactive-login")]
  public async Task<ActionResult<InteractiveLoginResponseDto>> InteractiveLogin(
    [FromServices] SignInManager<AppUser> signInManager,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] TimeProvider timeProvider,
    [FromServices] IOptionsMonitor<AppOptions> appOptions,
    [FromServices] IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
    [FromBody] LoginRequestDto request)
  {
    if (!appOptions.CurrentValue.EnableInteractiveBearerLogin)
    {
      return NotFound();
    }

    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
      return Unauthorized();
    }

    var result = await signInManager.CheckPasswordSignInAsync(
      user,
      request.Password,
      lockoutOnFailure: false);

    if (result.IsLockedOut)
    {
      return Ok(new InteractiveLoginResponseDto(RequiresTwoFactor: false, IsLockedOut: true));
    }

    if (result.Succeeded && user.RequirePasswordChange)
    {
      await userManager.ResetAccessFailedCountAsync(user);
      return Ok(new InteractiveLoginResponseDto(RequiresTwoFactor: false, RequiresPasswordChange: true));
    }

    var requiresTwoFactor = result.Succeeded && user.TwoFactorEnabled;

    if (requiresTwoFactor &&
        string.IsNullOrWhiteSpace(request.TwoFactorCode) &&
        string.IsNullOrWhiteSpace(request.TwoFactorRecoveryCode))
    {
      return Ok(new InteractiveLoginResponseDto(RequiresTwoFactor: true));
    }

    if (requiresTwoFactor)
    {
      if (!string.IsNullOrWhiteSpace(request.TwoFactorRecoveryCode))
      {
        var recoveryCodeResult = await userManager.RedeemTwoFactorRecoveryCodeAsync(
          user,
          request.TwoFactorRecoveryCode.Replace(" ", string.Empty).Replace("-", string.Empty));

        if (!recoveryCodeResult.Succeeded)
        {
          result = Microsoft.AspNetCore.Identity.SignInResult.Failed;
        }
        else
        {
          result = Microsoft.AspNetCore.Identity.SignInResult.Success;
        }
      }
      else if (!string.IsNullOrWhiteSpace(request.TwoFactorCode))
      {
        var normalizedCode = request.TwoFactorCode
          .Replace(" ", string.Empty)
          .Replace("-", string.Empty);

        var isValid = await userManager.VerifyTwoFactorTokenAsync(
          user,
          userManager.Options.Tokens.AuthenticatorTokenProvider,
          normalizedCode);

        if (!isValid)
        {
          result = Microsoft.AspNetCore.Identity.SignInResult.Failed;
        }
        else
        {
          result = Microsoft.AspNetCore.Identity.SignInResult.Success;
        }
      }

    if (result.Succeeded)
    {
      await userManager.ResetAccessFailedCountAsync(user);
      await userManager.UpdateLastLoginAsync(user);
    }
    }

    if (!result.Succeeded)
    {
      await userManager.AccessFailedAsync(user);

      if (await userManager.IsLockedOutAsync(user))
      {
        return Ok(new InteractiveLoginResponseDto(RequiresTwoFactor: false, IsLockedOut: true));
      }

      return Unauthorized();
    }

    var principal = await signInManager.CreateUserPrincipalAsync(user);
    var tokens = CreateInteractiveLoginTokens(
      principal,
      bearerTokenOptions.Get(IdentityConstants.BearerScheme),
      timeProvider);

    return Ok(new InteractiveLoginResponseDto(RequiresTwoFactor: false, RequiresPasswordChange: user.RequirePasswordChange, Tokens: tokens));
  }

  // This is used by web frontend, which uses cookie authentication.
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
      ExpiresInSeconds: (int)options.BearerTokenExpiration.TotalSeconds,
      RefreshToken: options.RefreshTokenProtector.Protect(refreshTicket));
  }
}
