using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace ControlR.ApiClient;

/// <summary>
/// Provides a factory for creating instances of <see cref="ControlrApiClient"/> configured with a specified HttpClient.
/// </summary>
/// <param name="httpClient">The HttpClient instance used to send HTTP requests for all created ControlrApiClient instances. Cannot be null.</param>
public interface IControlrApiClientFactory
{
  ControlrApiClient GetClient();
}

public class ControlrApiClientFactory(HttpClient httpClient) : IControlrApiClientFactory
{
  private readonly IAuthenticationProvider _authenticationProvider = new AnonymousAuthenticationProvider();
  private readonly HttpClient _httpClient = httpClient;

  public ControlrApiClient GetClient()
  {
    return new ControlrApiClient(new HttpClientRequestAdapter(_authenticationProvider, httpClient: _httpClient));
  }
}
