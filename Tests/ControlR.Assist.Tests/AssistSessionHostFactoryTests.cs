using ControlR.Assist.Services;
using ControlR.Libraries.Avalonia.Services;
using ControlR.Libraries.Shared.Services.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Tests;

public class AssistSessionHostFactoryTests
{
  [Fact]
  public void CreateSessionHost_BuildsAValidatedDependencyGraph()
  {
    // The Linux platform registration refuses to run without a desktop environment, and Wayland is
    // the path that needs IAsyncLock forwarded into the session host.
    var previousDisplay = Environment.GetEnvironmentVariable("DISPLAY");
    var previousWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
    Environment.SetEnvironmentVariable("DISPLAY", null);
    Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");

    try
    {
      using var appServices = new ServiceCollection()
        .AddLogging()
        .AddSingleton<IUiDispatcher, UiDispatcher>()
        .AddSingleton<IAsyncLock, KeyedLock>()
        .BuildServiceProvider();

      var factory = new AssistSessionHostFactory(
        appServices.GetRequiredService<ILoggerFactory>(),
        appServices.GetRequiredService<IUiDispatcher>(),
        appServices.GetRequiredService<IAsyncLock>());

      // Reaching this line means ValidateOnBuild accepted the graph, so every service the platform
      // registration needs was forwarded from the app container.
      using var host = factory.CreateSessionHost(new AssistSessionRequest(
        new Uri("https://server.example.com"),
        Guid.NewGuid(),
        "access-token",
        "Viewer",
        null));

      Assert.NotNull(host.Services);
    }
    finally
    {
      Environment.SetEnvironmentVariable("DISPLAY", previousDisplay);
      Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", previousWaylandDisplay);
    }
  }
}