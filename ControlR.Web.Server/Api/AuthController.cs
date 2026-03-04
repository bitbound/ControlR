using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.AuthEndpoint)]
[ApiController]
[Authorize]
public class LogoutController : ControllerBase
{
  [HttpPost("logout")]
  public async Task<IActionResult> Logout(
    [FromServices] SignInManager<AppUser> signInManager)
  {
    await signInManager.SignOutAsync();
    return NoContent();
  }
}
