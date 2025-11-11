using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.ServerStatsEndpoint)]
[ApiController]
[Authorize(Roles = RoleNames.ServerAdministrator)]
public class ServerStatsController(IServerStatsProvider serverStatsProvider) : ControllerBase
{
  private readonly IServerStatsProvider _serverStatsProvider = serverStatsProvider;

  [HttpGet]
  public async Task<ActionResult<ServerStatsDto>> GetServerStats()
  {
    var result = await _serverStatsProvider.GetServerStats();
    if (result.IsSuccess)
    {
      return result.Value;
    }

    return StatusCode(500, result.Reason);
  }
}
