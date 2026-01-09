using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Common;
public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddCommonDesktopServices<TToasterImpl>(
    this IHostApplicationBuilder builder,
    IIpcClientAccessor ipcClientAccessor,
    IUserInteractionService userInteractionService,
    Action<IHostApplicationBuilder> configureDependencies,
    Action<RemoteControlSessionOptions> configureSessionOptions,
    Action<DesktopClientOptions> configureDesktopClientOptions)
    where TToasterImpl : class, IToaster
  {
    builder.Configuration.AddEnvironmentVariables();

    configureDependencies(builder);

    builder.Services
      .AddOptions()
      .AddTransient<IHubConnectionBuilder, HubConnectionBuilder>()
      .AddSingleton(WeakReferenceMessenger.Default)
      .AddSingleton(TimeProvider.System)
      .AddSingleton<IProcessManager, ProcessManager>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<IImageUtility, ImageUtility>()
      .AddSingleton<IFrameEncoder, JpegEncoder>()
      .AddSingleton<IStreamEncoder, Vp9Encoder>()
      .AddSingleton<IMemoryProvider, MemoryProvider>()
      .AddSingleton<ISystemEnvironment, SystemEnvironment>()
      .AddSingleton<IDesktopRemoteControlStream, DesktopRemoteControlStream>()
      .AddSingleton<IDesktopCapturerFactory, DesktopCapturerFactory>()
      .AddSingleton<IDesktopPreviewProvider, DesktopPreviewProvider>()
      .AddSingleton<ISessionConsentService, SessionConsentService>()
      .AddSingleton<IWaiter, Waiter>()
      .AddSingleton<IToaster, TToasterImpl>()
      .AddSingleton(ipcClientAccessor)
      .AddSingleton(userInteractionService)
      .AddTransient<FrameBasedCapturer>()
      .AddTransient<StreamBasedCapturer>()
      .AddHostedService<HostLifetimeEventResponder>()
      .AddHostedService<RemoteControlSessionInitializer>()
      .Configure(configureSessionOptions)
      .Configure(configureDesktopClientOptions);

    return builder;
  }
}
