using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.DesktopClient.Common.State;
using ControlR.Libraries.Hosting;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.WebSocketRelay.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common;

public static class ServiceRegistrationExtensions
{
  public static IHostApplicationBuilder AddCommonRemoteControlServices(
    this IHostApplicationBuilder builder,
    RemoteControlHostDependencies dependencies,
    Action<RemoteControlSessionOptions> configureSessionOptions,
    Action<DesktopClientOptions> configureDesktopClientOptions)
  {
    builder.Configuration.AddEnvironmentVariables();

    builder.Services
      .AddSingleton(dependencies.Toaster)
      .AddSingleton(dependencies.UserInteraction)
      .AddSingleton(dependencies.DtoSender);

    if (dependencies.Dispatcher is not null)
    {
      builder.Services.AddSingleton(dependencies.Dispatcher);
    }

    if (dependencies.AsyncLock is not null)
    {
      builder.Services.AddSingleton(dependencies.AsyncLock);
    }

    if (dependencies.Consent is not null)
    {
      builder.Services.AddSingleton(dependencies.Consent);
    }

    builder.Services
      .AddOptions()
      .AddTransient<IHubConnectionBuilder, HubConnectionBuilder>()
      .AddSingleton<IMessenger>(new WeakReferenceMessenger())
      .AddSingleton(TimeProvider.System)
      .AddSingleton<IProcessManager, ProcessManager>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<IImageUtility, ImageUtility>()
      .AddSingleton<IStreamMetrics, StreamMetrics>()
      .AddSingleton<IStreamEncoder, Vp9Encoder>()
      .AddSingleton<IMemoryProvider, MemoryProvider>()
      .AddSingleton<ISystemEnvironment, SystemEnvironment>()
      .AddSingleton<IDesktopRemoteControlStream, DesktopRemoteControlStream>()
      .AddSingleton<RemoteControlViewerCommandHandler>()
      .AddSingleton<IRemoteControlViewerCommandHandler>(sp => new CommandPolicyViewerCommandHandler(
        sp.GetRequiredService<RemoteControlViewerCommandHandler>(),
        sp.GetRequiredService<IOptions<RemoteControlSessionOptions>>(),
        sp.GetRequiredService<ILogger<CommandPolicyViewerCommandHandler>>()))
      .AddSingleton<IDesktopCapturerFactory, DesktopCapturerFactory>()
      .AddSingleton<IFrameEncoder, SkiaSharpEncoder>()
      .AddSingleton<IDesktopPreviewProvider, DesktopPreviewProvider>();

    // TryAdd so an app that supplies its own consent service through
    // RemoteControlHostDependencies wins over this default.
    builder.Services.TryAddSingleton<ISessionConsentService, SessionConsentService>();

    builder.Services
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

  /// <summary>
  /// Validates the session host graph when it is built, so a dependency the app forgot to forward
  /// fails at construction instead of at StartAsync inside a live session.
  /// </summary>
  public static IHostApplicationBuilder ConfigureRemoteControlHostValidation(
    this IHostApplicationBuilder builder)
  {
    builder.ConfigureContainer(new DefaultServiceProviderFactory(
      new ServiceProviderOptions
      {
        ValidateOnBuild = true,
        ValidateScopes = true
      }));

    return builder;
  }
}