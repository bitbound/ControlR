using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerStats;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Server statistics snapshot: tenant, agent, and user counts.
/// </summary>
[Route(HttpConstants.V1.ServerStatsEndpoint)]
[ApiController]
[Authorize(Policy = PolicyNames.RequireServerTelemetryRead)]
[ApiVersion(ApiVersions.V1)]
public class ServerStatsController(IServerStatsProvider serverStatsProvider) : ControllerBase
{
  private readonly IServerStatsProvider _serverStatsProvider = serverStatsProvider;

  [HttpGet]
  [ProducesResponseType<ServerStatsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  public async Task<ActionResult<ServerStatsDto>> GetServerStats()
  {
    var result = await _serverStatsProvider.GetServerStats();

    if (!result.IsSuccess)
    {
      return Problem(
        detail: result.Reason,
        statusCode: StatusCodes.Status500InternalServerError);
    }

    var stats = result.Value;
    return new ServerStatsDto(
      stats.TotalTenants,
      stats.OnlineAgents,
      stats.TotalAgents,
      stats.OnlineUsers,
      stats.TotalUsers);
  }
}
