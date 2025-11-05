using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.ServerAlertEndpoint)]
[ApiController]
[Authorize]
public class ServerAlertController(AppDb appDb) : ControllerBase
{
  private static readonly Guid _singletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

  private readonly AppDb _appDb = appDb;


  [HttpGet]
  public async Task<ActionResult<ServerAlertResponseDto>> GetAlert()
  {
    var alert = await _appDb.ServerAlerts
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == _singletonId);

    if (alert is null)
    {
      return NotFound();
    }

    return alert.ToDto();
  }

  [HttpPost]
  [Authorize(Roles = RoleNames.ServerAdministrator)]
  public async Task<ActionResult<ServerAlertResponseDto>> UpdateAlert([FromBody] ServerAlertRequestDto request)
  {
    var alert = await _appDb
      .ServerAlerts
      .FirstOrDefaultAsync(x => x.Id == _singletonId);

    if (alert is null)
    {
      // Create the alert if it doesn't exist (shouldn't happen due to seeding, but be defensive)
      alert = new ServerAlert
      {
        Id = _singletonId,
        Message = request.Message,
        Severity = request.Severity,
        IsDismissable = request.IsDismissable,
        IsSticky = request.IsSticky,
        IsEnabled = request.IsEnabled
      };
      _appDb.ServerAlerts.Add(alert);
    }
    else
    {
      // Update existing
      alert.Message = request.Message;
      alert.Severity = request.Severity;
      alert.IsDismissable = request.IsDismissable;
      alert.IsSticky = request.IsSticky;
      alert.IsEnabled = request.IsEnabled;
    }

    await _appDb.SaveChangesAsync();
    return alert.ToDto();
  }
}
