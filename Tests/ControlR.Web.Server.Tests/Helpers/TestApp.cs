using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests.Helpers;

public class TestApp(
  WebApplication app,
  FakeTimeProvider timeProvider) : IAsyncDisposable
{
  public WebApplication App { get; } = app;
  public IServiceProvider Services => App.Services;
  public FakeTimeProvider TimeProvider { get; } = timeProvider;
  public async ValueTask DisposeAsync()
  {
    await App.DisposeAsync();
    GC.SuppressFinalize(this);
  }
}