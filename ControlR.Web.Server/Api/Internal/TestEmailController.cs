using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TestEmailEndpoint)]
[ApiController]
[Authorize(Roles = RoleNames.ServerAdministrator)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TestEmailController() : ControllerBase
{

  [HttpPost]
  public async Task<IActionResult> SendTestEmail(
    AppDb appDb,
    IControlrEmailSender emailSender,
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

    var result = await emailSender.SendEmailWithResult(
      user.Email,
      "ControlR Test Email",
      "<h1>Test Email</h1>" +
        "<p>This is a test email from your ControlR server.</p>");

    if (result.IsSuccess)
    {
      return Ok();
    }

    return Problem(result.Reason);
  }
}
