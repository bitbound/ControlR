using Microsoft.Extensions.Logging;
using System.Net.Http.Json;


namespace ControlR.Libraries.Shared.Services.Http;

public interface IVersionApi
{
    Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime);
    Task<Result<Version>> GetCurrentAgentVersion();

    Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime);

    Task<Result<Version>> GetCurrentViewerVersion();
}

internal class VersionApi(
    HttpClient client,
    ILogger<VersionApi> logger) : IVersionApi
{
    private readonly string _agentVersionEndpoint = "/api/version/agent";
    private readonly HttpClient _client = client;
    private readonly ILogger<VersionApi> _logger = logger;
    private readonly string _viewerVersionEndpoint = "/api/version/viewer";

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
            var version = await _client.GetFromJsonAsync<Version>(_agentVersionEndpoint);
            _logger.LogInformation("Latest Agent version on server: {LatestAgentVersion}", version);
            if (version is null)
            {
                return Result.Fail<Version>("Server version response was empty.");
            }
            return Result.Ok(version);
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
            var version = await _client.GetFromJsonAsync<Version>(_viewerVersionEndpoint);
            _logger.LogInformation("Latest viewer version on server: {LatestViewerVersion}", version);
            if (version is null)
            {
                return Result.Fail<Version>("Server version response was empty.");
            }
            return Result.Ok(version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for new viewer versions.");
            return Result.Fail<Version>(ex);
        }
    }
}