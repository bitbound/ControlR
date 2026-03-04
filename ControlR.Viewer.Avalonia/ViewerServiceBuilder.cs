using Avalonia.Input.Platform;
using ControlR.ApiClient;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Signalr.Client.Extensions;
using ControlR.Viewer.Avalonia.ViewModels.Dialogs;
using ControlR.Viewer.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Viewer.Avalonia;

/// <summary>
///   Builds service providers for individual <see cref="ControlrViewer"/> instances.
///   Each instance gets its own isolated DI container.
/// </summary>
public static class ViewerServiceBuilder
{
  private static Action<ILoggingBuilder>? _configureLogging;

  /// <summary>
  ///   Call this method at application startup, before any <see cref="ControlrViewer"/> instances are created,
  ///   to provide configuration that will apply to all viewer instances and their internal services.
  /// </summary>
  /// <param name="configureLogging">
  ///   Supply a custom logging configuration.  By default, the Console provider is used with a minimum log level of Information.
  /// </param>
  public static void Configure(Action<ILoggingBuilder>? configureLogging = null)
  {
    _configureLogging = configureLogging;
  }

  internal static IServiceProvider BuildServiceProvider(ControlrViewerOptions viewerOptions, Guid instanceId, IClipboard clipboard)
  {
    ArgumentNullException.ThrowIfNull(viewerOptions);

    var services = new ServiceCollection();

    // Register options.
    services
     .AddOptions<ControlrViewerOptions>()
      .Configure(opts =>
      {
        opts.DeviceId = viewerOptions.DeviceId;
        opts.BaseUrl = viewerOptions.BaseUrl;
        opts.PersonalAccessToken = viewerOptions.PersonalAccessToken;
      })
     .Validate(options => options.DeviceId != Guid.Empty, "DeviceId is required.")
     .Validate(options => options.BaseUrl is not null, "BaseUrl is required.")
     .Validate(options => !string.IsNullOrWhiteSpace(options.PersonalAccessToken), "PersonalAccessToken is required.")
     .ValidateOnStart();

    // Register logging.
    services.AddLogging(builder =>
    {
      if (_configureLogging is not null)
      {
        _configureLogging.Invoke(builder);
      }
      else
      {
        builder
          .AddConsole()
          .SetMinimumLevel(LogLevel.Information);
      }
    });

    // Register core services.
    services.AddSingleton<IMessenger, WeakReferenceMessenger>();
    services.AddSingleton<IMemoryProvider, MemoryProvider>();
    services.AddSingleton<IWaiter, Waiter>();
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton(clipboard);
    services.AddSingleton<IInstanceIdProvider>(new InstanceIdProvider(instanceId));

    // Add API client.
    services.AddControlrApiClient(options =>
    {
      options.BaseUrl = viewerOptions.BaseUrl;
      options.PersonalAccessToken = viewerOptions.PersonalAccessToken;
    });

    // Register state services.
    services.AddSingleton<IDeviceState, DeviceState>();
    services.AddSingleton<IChatState, ChatState>();
    services.AddSingleton<IMetricsState, MetricsState>();
    services.AddSingleton<ITerminalState, TerminalState>();
    services.AddSingleton<IRemoteControlState, RemoteControlState>();
    services.AddSingleton<IViewerRemoteControlStream, ViewerRemoteControlStream>();
    
    // Register SignalR hub client.
    services.AddStronglyTypedSignalrClient<IViewerHub, IViewerHubClient, ViewerHubClient>(ServiceLifetime.Singleton);
    services.AddSingleton<IViewerHubConnector, ViewerHubConnector>();

    // Register navigation service.
    services.AddSingleton<INavigationProvider, NavigationProvider>();

    // Register ViewModels.
    services.AddSingleton<IViewerShellViewModel, ViewerShellViewModel>();
    services.AddSingleton<IRemoteControlViewModel, RemoteControlViewModel>();
    services.AddSingleton<IDesktopSessionViewModel, DesktopSessionViewModel>();
    services.AddSingleton<IRemoteDisplayViewModel, RemoteDisplayViewModel>();

    // Register Views.
    services.AddSingleton<ViewerShell>();
    services.AddSingleton<RemoteControlView>();
    services.AddSingleton<RemoteDisplayView>();
    services.AddTransient<DesktopPreviewDialogView>();

    services.AddControlrSnackbar();
    services.AddControlrDialogs();
    services.AddSingleton<IDesktopPreviewDialogViewModelFactory, DesktopPreviewDialogViewModelFactory>();

    return services.BuildServiceProvider();
  }
}
