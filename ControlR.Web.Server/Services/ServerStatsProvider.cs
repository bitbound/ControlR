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
  public Task<Result<ServerStatsDto>> GetServerStats()
  {
    try
    {
      var dto = new ServerStatsDto(
        connectionCounter.AgentConnectionCount,
        connectionCounter.ViewerConnectionCount);

      return Result.Ok(dto).AsTaskResult();
    }
    catch (Exception ex)
    {
      return Result
        .Fail<ServerStatsDto>(ex, "Error while getting server stats.")
        .Log(logger)
        .AsTaskResult();
    }
  }
}