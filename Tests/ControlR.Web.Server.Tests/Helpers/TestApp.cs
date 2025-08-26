using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;
using Xunit.Sdk;

namespace ControlR.Web.Server.Tests.Helpers;

internal class TestApp(
  FakeTimeProvider timeProvider,
  WebApplicationFactory<Program> factory,
  WebApplication webApp) : IAsyncDisposable
{
  private HttpClient? _httpClient;

  public WebApplicationFactory<Program> Factory { get; } = factory;
  /// <summary>
  /// <para>
  ///   This service provider is the same as the one in <see cref="App"/>.
  ///   However, it's completely separate from <see cref="TestServer"/>'s service provider.
  /// </para>
  /// <para>
  ///   Use this or <see cref="App"/> if you only need access to services.
  ///   Use <see cref="TestServer"/> if you need full integration testing
  ///   of endpoints, although it is slower.
  /// </para>
  /// </summary>
  public IServiceProvider Services => App.Services;

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

  /// <summary>
  /// <para>
  ///   The service provider in <see cref="App"/> is the same as the one in <see cref="Services"/>.
  ///   However, it is completely separate from the one in <see cref="TestServer"/>.
  /// </para>
  /// <para>
  ///   Use this or <see cref="App"/> if you only need access to services.
  ///   Use <see cref="TestServer"/> if you need full integration testing
  ///   of endpoints, although it is slower.
  /// </para>
  /// </summary>
  public WebApplication App { get; } = webApp;
  public async ValueTask DisposeAsync()
  {
    _httpClient?.Dispose();
    Factory.Dispose();
    TestServer.Dispose();
    await App.DisposeAsync();
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