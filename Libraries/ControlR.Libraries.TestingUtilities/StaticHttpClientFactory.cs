namespace ControlR.Libraries.TestingUtilities;

public sealed class StaticHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
{
  private readonly HttpClient _httpClient = httpClient;

  public HttpClient CreateClient(string name) => _httpClient;
}