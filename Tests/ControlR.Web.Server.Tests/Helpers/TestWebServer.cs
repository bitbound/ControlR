using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests.Helpers;

internal class TestWebServer(
  FakeTimeProvider timeProvider,
  WebApplicationFactory<Program> factory) : IDisposable
{
  private HttpClient? _httpClient;

  public WebApplicationFactory<Program> Factory { get; } = factory;
  public IServiceProvider Services => TestServer.Services;
  public TestServer TestServer => Factory.Server;

  public FakeTimeProvider TimeProvider { get; } = timeProvider;

  public void Dispose()
  {
    _httpClient?.Dispose();
    Factory.Dispose();
    TestServer.Dispose();
    GC.SuppressFinalize(this);
  }

  public async Task<HttpClient> GetHttpClient()
  {
    _httpClient ??= TestServer.CreateClient();
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      _ = await _httpClient.GetAsync("/health", HttpCompletionOption.ResponseHeadersRead, cts.Token);
    }
    catch
    {
      // Ignore.
    }
    return _httpClient;
  }
}