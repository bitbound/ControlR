using ControlR.ApiClient;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.TestingUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Creates a fully configured <see cref="IControlrApi"/> client that routes through a <see cref="TestWebServer"/>,
/// enabling integration tests to exercise the API client with real HTTP requests to the test server.
/// </summary>
internal sealed class TestControlrApiClient : IDisposable
{
  private readonly HttpClient _apiHttpClient;
  private readonly HttpClient _unauthenticatedHttpClient;

  public TestControlrApiClient(TestWebServer testServer)
  {
    var baseUrl = new Uri("http://localhost");
    AuthState = new ControlrApiClientAuthState();

    _unauthenticatedHttpClient = new HttpClient(testServer.TestServer.CreateHandler())
    {
      BaseAddress = baseUrl
    };

    var httpClientFactory = new StaticHttpClientFactory(_unauthenticatedHttpClient);
    var options = new ControlrApiClientOptions
    {
      BaseUrl = baseUrl
    };

    var authHeaderHandler = new ControlrApiAuthHeaderHandler(AuthState)
    {
      InnerHandler = testServer.TestServer.CreateHandler()
    };

    _apiHttpClient = new HttpClient(authHeaderHandler)
    {
      BaseAddress = baseUrl
    };

    var bearerTokenRefresher = new BearerTokenRefresher(AuthState, httpClientFactory, testServer.TimeProvider);

    AuthSession = new ControlrAuthSession(
      httpClientFactory,
      AuthState,
      bearerTokenRefresher,
      NullLogger<ControlrAuthSession>.Instance,
      new OptionsMonitorWrapper<ControlrApiClientOptions>(options),
      testServer.TimeProvider);

    Api = new ControlrApi(
      _apiHttpClient,
      AuthState,
      bearerTokenRefresher,
      NullLogger<ControlrApi>.Instance,
      new OptionsWrapper<ControlrApiClientOptions>(options)).InternalApi;
  }

  /// <summary>
  /// The API client instance, providing access to Internal API surface methods.
  /// </summary>
  public IControlrInternalApi Api { get; }

  /// <summary>
  /// The underlying auth session, useful for inspecting or manipulating auth state.
  /// </summary>
  public ControlrAuthSession AuthSession { get; }

  /// <summary>
  /// The auth state, useful for inspecting tokens or snapshots.
  /// </summary>
  public ControlrApiClientAuthState AuthState { get; }

  public void Dispose()
  {
    AuthSession.Dispose();
    _apiHttpClient.Dispose();
    _unauthenticatedHttpClient.Dispose();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Stops the background token-refresh loop without disposing the underlying HTTP clients.
  /// Call this in test cleanup when the test does not need to dispose the full <see cref="TestControlrApiClient"/>.
  /// </summary>
  public void StopBackgroundRefresh()
  {
    AuthSession.Dispose();
  }
}