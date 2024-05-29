using Microsoft.Extensions.Logging;

namespace ControlR.Shared.Services.Http;

public interface IVersionApi
{
    Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime);
    Task<Result<Version>> GetCurrentAgentVersion();

    Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime);

    Task<Result<Version>> GetCurrentViewerVersion();
}

internal class VersionApi(
    HttpClient client,
    ILogger<KeyApi> logger) : IVersionApi
{
    private readonly string _agentVersionFile = "/downloads/AgentVersion.txt";
    private readonly HttpClient _client = client;
    private readonly ILogger<KeyApi> _logger = logger;
    private readonly string _viewerVersionFile = "/downloads/ViewerVersion.txt";

    public async Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime)
    {
        try
        {
            var fileRelativePath = AppConstants.GetAgentFileDownloadPath(runtime);
            using var request = new HttpRequestMessage(HttpMethod.Head, fileRelativePath);
            using var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentMD5 is byte[] contentMd5)
            {
                return Result.Ok(contentMd5);
            }
            
            return Result.Fail<byte[]>("Failed to get agent file hash.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for new agent hash.");
            return Result.Fail<byte[]>(ex);
        }
    }

    public async Task<Result<Version>> GetCurrentAgentVersion()
    {
        try
        {
            var versionResult = await _client.GetStringAsync(_agentVersionFile);

            _logger.LogInformation("Latest Agent version on server: {LatestAgentVersion}", versionResult);
            if (!Version.TryParse(versionResult?.Trim(), out var agentVersion))
            {
                var result = Result.Fail<Version>("Failed to get latest version from server.");
                _logger.LogResult(result);
                return result;
            }
            return Result.Ok(agentVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for new agent versions.");
            return Result.Fail<Version>(ex);
        }
    }

    public async Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime)
    {
        try
        {
            var fileRelativePath = AppConstants.GetStreamerFileDownloadPath(runtime);
            using var request = new HttpRequestMessage(HttpMethod.Head, fileRelativePath);
            using var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentMD5 is byte[] contentMd5)
            {
                return Result.Ok(contentMd5);
            }

            return Result.Fail<byte[]>("Failed to get streamer file hash.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for new streamer hash.");
            return Result.Fail<byte[]>(ex);
        }
    }
    public async Task<Result<Version>> GetCurrentViewerVersion()
    {
        try
        {
            var versionResult = await _client.GetStringAsync(_viewerVersionFile);

            _logger.LogInformation("Latest viewer version on server: {LatestViewerVersion}", versionResult);
            if (!Version.TryParse(versionResult?.Trim(), out var viewerVersion))
            {
                var result = Result.Fail<Version>("Failed to get latest version from server.");
                _logger.LogResult(result);
                return result;
            }
            return Result.Ok(viewerVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for new viewer versions.");
            return Result.Fail<Version>(ex);
        }
    }
}