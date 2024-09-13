using ControlR.Web.Server.Services.Interfaces;

namespace ControlR.Web.Server.Services;

public interface IServerStatsProvider
{
    Task<Result<ServerStatsDto>> GetServerStats();
}

public class ServerStatsProvider(
    IConnectionCounter _connectionCounter,
    ILogger<ServerStatsProvider> _logger) : IServerStatsProvider
{
    private string? _appVersion;

    public async Task<Result<ServerStatsDto>> GetServerStats()
    {
        try
        {
            _appVersion ??= GetAppVersion();
            var agentResult = await _connectionCounter.GetAgentConnectionCount();
            var viewerResult = await _connectionCounter.GetViewerConnectionCount();

            if (!agentResult.IsSuccess)
            {
                _logger.LogResult(agentResult);
                return Result.Fail<ServerStatsDto>(agentResult.Reason);
            }

            if (!viewerResult.IsSuccess)
            {
                _logger.LogResult(viewerResult);
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
                .Log(_logger);
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
            _logger.LogError(ex, "Failed to get app version.");
            return defaultVersion;
        }
    }
}