using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.DesktopClient.Common.State;
using ControlR.Libraries.Hosting;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Common;
public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddCommonDesktopServices(
    this IHostApplicationBuilder builder,
    Action<IHostApplicationBuilder> configureDependencies,
    Action<RemoteControlSessionOptions> configureSessionOptions,
    Action<DesktopClientOptions> configureDesktopClientOptions)
  {
    builder.Configuration.AddEnvironmentVariables();

    configureDependencies(builder);

    builder.Services
      .AddOptions()
      .AddTransient<IHubConnectionBuilder, HubConnectionBuilder>()
      .AddSingleton<IMessenger>(new WeakReferenceMessenger())
      .AddSingleton(TimeProvider.System)
      .AddSingleton<IProcessManager, ProcessManager>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<IImageUtility, ImageUtility>()
      .AddSingleton<IStreamEncoder, Vp9Encoder>()
      .AddSingleton<IMemoryProvider, MemoryProvider>()
      .AddSingleton<ISystemEnvironment, SystemEnvironment>()
      .AddSingleton<IDesktopRemoteControlStream, DesktopRemoteControlStream>()
      .AddSingleton<IDesktopCapturerFactory, DesktopCapturerFactory>()
      .AddSingleton<IFrameEncoder, SkiaSharpEncoder>()
      .AddSingleton<IDesktopPreviewProvider, DesktopPreviewProvider>()
      .AddSingleton<ISessionConsentService, SessionConsentService>()
      .AddSingleton<IRemoteControlSessionState, RemoteControlSessionState>()
      .AddSingleton<IWaiter, Waiter>()
      .AddTransient<FrameBasedCapturer>()
      .AddTransient<StreamBasedCapturer>()
      .AddHostedService<HostLifetimeEventResponder>()
      .AddHostedService<RemoteControlSessionInitializer>()
      .Configure(configureSessionOptions)
      .Configure(configureDesktopClientOptions);

    return builder;
  }
}
