using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests.Helpers;

internal class TestApp(
  FakeTimeProvider timeProvider,
  WebApplication webApp) : IAsyncDisposable
{
  public WebApplication App { get; } = webApp;
  public IServiceProvider Services => App.Services;
  public FakeTimeProvider TimeProvider { get; } = timeProvider;

  public async ValueTask DisposeAsync()
  {
    await App.DisposeAsync();
    GC.SuppressFinalize(this);
  }
}