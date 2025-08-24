using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests.Helpers;

public class TestApp(
  WebApplication app,
  FakeTimeProvider timeProvider,
  HttpClient httpClient,
  TestServer testServer) : IAsyncDisposable
{
  public WebApplication App { get; } = app;
  public IServiceProvider Services => App.Services;
  public FakeTimeProvider TimeProvider { get; } = timeProvider;
  public HttpClient HttpClient { get; } = httpClient;
  public TestServer TestServer { get; } = testServer;

  public async ValueTask DisposeAsync()
  {
    HttpClient.Dispose();
    TestServer.Dispose();
    await App.DisposeAsync();
    GC.SuppressFinalize(this);
  }
}