using Microsoft.AspNetCore.Builder;
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

  /// <summary>
  /// <para>
  /// This service provider in <see cref="TestServer"/> is completely separate
  /// from the one in <see cref="App"/> and <see cref="Services"/>.
  /// </para>
  /// <para>
  ///   Use this if you need full integration testing of endpoints, although it is slower.
  ///   Use <see cref="Services"/> or <see cref="App"/> if you only need access to services.
  /// </para>
  /// </summary>
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