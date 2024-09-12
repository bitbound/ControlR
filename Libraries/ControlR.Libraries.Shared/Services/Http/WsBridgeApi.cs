namespace ControlR.Libraries.Shared.Services.Http;

public interface IWsBridgeApi
{
    Task<bool> IsHealthy(Uri origin);
}

internal class WsBridgeApi(
    HttpClient _client,
    ILogger<KeyApi> _logger) : IWsBridgeApi
{
    public async Task<bool> IsHealthy(Uri origin)
    {
        try
        {
            var healthUri = new Uri(origin.ToHttpUri(), "/api/health");
            using var response = await _client.GetAsync(healthUri);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking health.");
            return false;
        }
    }
}
