using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;
using Xunit.Sdk;

namespace ControlR.Web.Server.Tests.Helpers;

internal class TestApp(
  WebApplication app,
  FakeTimeProvider timeProvider,
  WebApplicationFactory<Program> factory) : IAsyncDisposable
{
  private HttpClient? _httpClient;

  public WebApplication App { get; } = app;
  public IServiceProvider Services => App.Services;
  public FakeTimeProvider TimeProvider { get; } = timeProvider;
  public WebApplicationFactory<Program> Factory { get; } = factory;
  public TestServer TestServer => Factory.Server;

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

  public async ValueTask DisposeAsync()
  {
    _httpClient?.Dispose();
    Factory.Dispose();
    TestServer.Dispose();
    await App.DisposeAsync();
    GC.SuppressFinalize(this);
  }
}