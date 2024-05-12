using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace ControlR.Shared.Services.Http;

public interface IVersionApi
{
    Task<Result<byte[]>> GetCurrentAgentHash();
    Task<Result<Version>> GetCurrentAgentVersion();

    Task<Result<byte[]>> GetCurrentStreamerHash();

    Task<Result<Version>> GetCurrentViewerVersion();
}

internal class VersionApi(
    HttpClient client,
    ILogger<KeyApi> logger) : IVersionApi
{
    private readonly string _agentBinaryPath = $"/downloads/{RuntimeInformation.RuntimeIdentifier}/{AppConstants.AgentFileName}";
    private readonly string _agentVersionFile = "/downloads/AgentVersion.txt";
    private readonly HttpClient _client = client;
    private readonly ILogger<KeyApi> _logger = logger;
    private readonly string _streamerZipPath = $"/downloads/{AppConstants.RemoteControlZipFileName}";
    private readonly string _viewerVersionFile = "/downloads/ViewerVersion.txt";

    public async Task<Result<byte[]>> GetCurrentAgentHash()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, _agentBinaryPath);
            using var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Got headers from remote agent file: {Headers}", response.Headers);
            if (response.Headers.TryGetValues("MD5", out var values))
            {
                var hash = Convert.FromBase64String(values.First());
                return Result.Ok(hash);
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

    public async Task<Result<byte[]>> GetCurrentStreamerHash()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, _streamerZipPath);
            using var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            if (response.Headers.TryGetValues("MD5", out var values))
            {
                var hash = Convert.FromBase64String(values.First());
                return Result.Ok(hash);
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