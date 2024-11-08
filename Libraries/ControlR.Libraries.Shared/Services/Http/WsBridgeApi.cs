namespace ControlR.Libraries.Shared.Services.Http;

public interface IWsBridgeApi
{
  Task<bool> IsHealthy(Uri origin);
}

internal class WsBridgeApi(
  HttpClient client,
  ILogger<WsBridgeApi> logger) : IWsBridgeApi
{
  public async Task<bool> IsHealthy(Uri origin)
  {
    try
    {
      var healthUri = new Uri(origin.ToHttpUri(), "/api/health");
      using var response = await client.GetAsync(healthUri);
      response.EnsureSuccessStatusCode();
      return true;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while checking health.");
      return false;
    }
  }
}