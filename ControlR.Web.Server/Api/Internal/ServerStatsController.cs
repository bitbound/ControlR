using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.ServerStatsEndpoint)]
[ApiController]
  [Authorize(Policy = PolicyNames.RequireServerTelemetryRead)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class ServerStatsController(IServerStatsProvider serverStatsProvider) : ControllerBase
{
  private readonly IServerStatsProvider _serverStatsProvider = serverStatsProvider;

  [HttpGet]
  public async Task<ActionResult<InternalDtos.ServerStatsDto>> GetServerStats()
  {
    var result = await _serverStatsProvider.GetServerStats();
    if (result.IsSuccess)
    {
      return result.Value;
    }

    return StatusCode(500, result.Reason);
  }
}
