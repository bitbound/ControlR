using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests.Helpers;

internal class TestApp(
  FakeTimeProvider timeProvider,
  WebApplication webApp) : IAsyncDisposable
{
  public WebApplication App { get; } = webApp;
  public IServiceProvider Services => App.Services;
  public FakeTimeProvider TimeProvider { get; } = timeProvider;

  /// <summary>
  /// Creates a new service scope for resolving scoped services.
  /// Dispose the returned scope when done.
  /// </summary>
  public IServiceScope CreateScope() => App.Services.CreateScope();

  public async ValueTask DisposeAsync()
  {
    await App.DisposeAsync();
    GC.SuppressFinalize(this);
  }
}