using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Common;
public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddCommonDesktopServices(
    this IHostApplicationBuilder builder,
    Action<StreamingSessionOptions> configureStartup)
  {
    builder.Configuration.AddEnvironmentVariables();

    builder.Services
      .AddOptions()
      .AddTransient<IHubConnectionBuilder, HubConnectionBuilder>()
      .AddSingleton(WeakReferenceMessenger.Default)
      .AddSingleton(TimeProvider.System)
      .AddSingleton<IProcessManager, ProcessManager>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<IImageUtility, ImageUtility>()
      .AddSingleton<IMemoryProvider, MemoryProvider>()
      .AddSingleton<ISystemEnvironment, SystemEnvironment>()
      .AddSingleton<IStreamerStreamingClient, StreamerStreamingClient>()
      .AddSingleton<IDesktopCapturer, DesktopCapturer>()
      .AddSingleton<IDelayer, Delayer>()
      .AddHostedService<HostLifetimeEventResponder>()
      .AddHostedService<DtoHandler>()
      .AddHostedService(x => x.GetRequiredService<IStreamerStreamingClient>())
      .Configure(configureStartup);

    return builder;
  }
}
