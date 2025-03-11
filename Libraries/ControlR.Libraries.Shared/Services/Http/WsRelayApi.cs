namespace ControlR.Libraries.Shared.Services.Http;

public interface IWsRelayApi
{
  Task<bool> IsHealthy(Uri origin);
}

internal class WsRelayApi(
  HttpClient client,
  ILogger<WsRelayApi> logger) : IWsRelayApi
{
  public async Task<bool> IsHealthy(Uri origin)
  {
    try
    {
      var healthUri = new Uri(origin.ToHttpUri(), "/health");
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