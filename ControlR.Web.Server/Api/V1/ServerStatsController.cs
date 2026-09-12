using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using StatsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerStats;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Server statistics snapshot: tenant, agent, and user counts. A diagnostics probe guarded by
/// the server telemetry read permission.
/// </summary>
[Route(HttpConstants.V1.ServerStatsEndpoint)]
[ApiController]
[Authorize(Policy = PolicyNames.RequireServerTelemetryRead)]
[ApiVersion(ApiVersions.V1)]
public class ServerStatsController(IServerStatsProvider serverStatsProvider) : ControllerBase
{
  private readonly IServerStatsProvider _serverStatsProvider = serverStatsProvider;

  [HttpGet]
  [ProducesResponseType<StatsDtos.ServerStatsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<StatsDtos.ServerStatsDto>> GetServerStats()
  {
    var result = await _serverStatsProvider.GetServerStats();

    if (!result.IsSuccess)
    {
      return Problem(
        detail: result.Reason,
        statusCode: StatusCodes.Status500InternalServerError);
    }

    var stats = result.Value;
    return new StatsDtos.ServerStatsDto(
      stats.TotalTenants,
      stats.OnlineAgents,
      stats.TotalAgents,
      stats.OnlineUsers,
      stats.TotalUsers);
  }
}
