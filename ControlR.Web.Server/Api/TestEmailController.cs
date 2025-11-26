using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.TestEmailEndpoint)]
[ApiController]
[Authorize(Roles = RoleNames.ServerAdministrator)]
public class TestEmailController() : ControllerBase
{

  [HttpPost]
  public async Task<IActionResult> SendTestEmail(
    AppDb appDb,
    IEmailSender emailSender,
    IOptionsMonitor<AppOptions> appOptions)
  {
    if (appOptions.CurrentValue.DisableEmailSending)
    {
      return BadRequest("Email sending is disabled in application settings.");
    }

    if (!User.TryGetUserId(out var userId))
    {
      return BadRequest("User ID not found");
    }

    var user = await appDb
      .Users
      .AsNoTracking()
      .Select(x => new { x.Email, x.Id })
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user?.Email is null)
    {
      return BadRequest("User email not found");
    }

    await emailSender.SendEmailAsync(
      user.Email,
      "ControlR Test Email",
      "<h1>Test Email</h1>" +
        "<p>This is a test email from your ControlR server.</p>");

    return Ok();
  }
}
