using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Web.Server.Services;

public interface IServerStatsProvider
{
  Task<Result<ServerStatsDto>> GetServerStats();
}

public class ServerStatsProvider(
  IConnectionCounter connectionCounter,
  ILogger<ServerStatsProvider> logger) : IServerStatsProvider
{
  public async Task<Result<ServerStatsDto>> GetServerStats()
  {
    try
    {
      var agentResult = await connectionCounter.GetAgentConnectionCount();
      var viewerResult = await connectionCounter.GetViewerConnectionCount();

      if (!agentResult.IsSuccess)
      {
        logger.LogResult(agentResult);
        return Result.Fail<ServerStatsDto>(agentResult.Reason);
      }

      if (!viewerResult.IsSuccess)
      {
        logger.LogResult(viewerResult);
        return Result.Fail<ServerStatsDto>(viewerResult.Reason);
      }

      var dto = new ServerStatsDto(
        agentResult.Value,
        viewerResult.Value);

      return Result.Ok(dto);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<ServerStatsDto>(ex, "Error while getting server stats.")
        .Log(logger);
    }
  }
}