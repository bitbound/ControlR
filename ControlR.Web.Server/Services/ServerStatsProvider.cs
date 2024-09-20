namespace ControlR.Web.Server.Services;

public interface IServerStatsProvider
{
  Task<Result<ServerStatsDto>> GetServerStats();
}

public class ServerStatsProvider(
  IConnectionCounter connectionCounter,
  ILogger<ServerStatsProvider> logger) : IServerStatsProvider
{
  private string? _appVersion;

  public async Task<Result<ServerStatsDto>> GetServerStats()
  {
    try
    {
      _appVersion ??= GetAppVersion();
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
        viewerResult.Value,
        _appVersion);

      return Result.Ok(dto);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<ServerStatsDto>(ex, "Error while getting server stats.")
        .Log(logger);
    }
  }

  private string GetAppVersion(string defaultVersion = "1.0.0")
  {
    try
    {
      return typeof(ServerStatsDto)
               .Assembly
               .GetName()
               ?.Version
               ?.ToString()
             ?? defaultVersion;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to get app version.");
      return defaultVersion;
    }
  }
}