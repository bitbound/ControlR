using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LogoutController : ControllerBase
{
  [HttpPost]
  public async Task<IActionResult> Logout(
    [FromBody] object _,
    [FromServices] SignInManager<AppUser> signInManager)
  {
    await signInManager.SignOutAsync();
    return NoContent();
  }
}
