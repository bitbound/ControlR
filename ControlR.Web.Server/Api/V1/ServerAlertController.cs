using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerAlerts;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Server-wide alert banner. A singleton resource: read the current alert, or replace it.
/// Replacing requires server settings write.
/// </summary>
[Route(HttpConstants.V1.ServerAlertEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class ServerAlertController(AppDb appDb) : ControllerBase
{
  private static readonly Guid _singletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

  private readonly AppDb _appDb = appDb;

  [HttpGet]
  [ProducesResponseType<ServerAlertResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ServerAlertResponseDto>> GetAlert()
  {
    var alert = await _appDb.ServerAlerts
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == _singletonId);

    if (alert is null)
    {
      return NotFound();
    }

    return ToResponseDto(alert);
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireServerSettingsWrite)]
  [ProducesResponseType<ServerAlertResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<ServerAlertResponseDto>> UpdateAlert(
    [FromBody] ServerAlertRequestDto request)
  {
    var alert = await _appDb
      .ServerAlerts
      .FirstOrDefaultAsync(x => x.Id == _singletonId);

    if (alert is null)
    {
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
      alert.Message = request.Message;
      alert.Severity = request.Severity;
      alert.IsDismissable = request.IsDismissable;
      alert.IsSticky = request.IsSticky;
      alert.IsEnabled = request.IsEnabled;
    }

    await _appDb.SaveChangesAsync();
    return ToResponseDto(alert);
  }

  private static ServerAlertResponseDto ToResponseDto(ServerAlert alert)
  {
    return new ServerAlertResponseDto(
      alert.Id,
      alert.Message,
      alert.Severity,
      alert.IsDismissable,
      alert.IsSticky,
      alert.IsEnabled);
  }
}
