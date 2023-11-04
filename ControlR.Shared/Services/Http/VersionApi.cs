using ControlR.Shared.Extensions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ControlR.Shared.Services.Http;

public interface IVersionApi
{
    Task<Result<Version>> GetCurrentViewerVersion();
}

internal class VersionApi(
    HttpClient client,
    ILogger<KeyApi> logger) : IVersionApi
{
    private readonly HttpClient _client = client;
    private readonly ILogger<KeyApi> _logger = logger;

    public async Task<Result<Version>> GetCurrentViewerVersion()
    {
        try
        {
            using var response = await _client.GetAsync($"/api/version/viewer");
            response.EnsureSuccessStatusCode();
            var currentVersion = await response.Content.ReadFromJsonAsync<Version>();
            if (currentVersion is null)
            {
                var result = Result.Fail<Version>("Failed to deserialize version while checking for updates.");
                _logger.LogResult(result);
                return result;
            }
            return Result.Ok(currentVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while verifying authentication.");
            return Result.Fail<Version>(ex);
        }
    }
}