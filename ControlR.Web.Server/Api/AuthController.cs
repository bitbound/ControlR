using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

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
  [HttpPost("forgot-password")]
  public async Task<IActionResult> ForgotPassword(
    [FromServices] IPasswordManager passwordManager,
    [FromBody] ForgotPasswordRequestDto request)
  {
    var resetPasswordUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Account/ResetPassword";
    var result = await passwordManager.ForgotPassword(request, resetPasswordUrl);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }

  [Authorize]
  [HttpPost("logout")]
  public async Task<IActionResult> Logout(
    [FromServices] SignInManager<AppUser> signInManager)
  {
    await signInManager.SignOutAsync();
    return NoContent();
  }

  [AllowAnonymous]
  [HttpPost("reset-password")]
  public async Task<IActionResult> ResetPassword(
    [FromServices] IPasswordManager passwordManager,
    [FromBody] ResetPasswordRequestDto request)
  {
    var result = await passwordManager.ResetPassword(request);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok();
  }
}
