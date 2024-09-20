namespace ControlR.Libraries.Shared.Services.Http;

public interface IReleasesApi
{
  Task<bool> DoesReleaseHashExist(string releaseHexHash);
}

internal class ReleasesApi(
  HttpClient client,
  ILogger<ReleasesApi> logger) : IReleasesApi
{
  private readonly Uri _baseUri = new("https://releases.controlr.app");

  public async Task<bool> DoesReleaseHashExist(string releaseHexHash)
  {
    try
    {
      using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(_baseUri, releaseHexHash));
      using var response = await client.SendAsync(request);
      response.EnsureSuccessStatusCode();
      return true;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while verifying release hash.  Hash: {ReleaseHash}.", releaseHexHash);
      return false;
    }
  }
}