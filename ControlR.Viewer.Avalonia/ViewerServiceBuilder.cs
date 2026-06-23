using Avalonia.Input.Platform;
using ControlR.ApiClient;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Signalr.Client.Extensions;
using ControlR.Libraries.Viewer.Common.Options;
using ControlR.Viewer.Avalonia.Services.Navigation;
using ControlR.Viewer.Avalonia.ViewModels.Dialogs;
using ControlR.Viewer.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    ValidateViewerOptions(viewerOptions);

    // Register options by reference so the host app can switch auth modes before reconnecting.
    services.AddSingleton(viewerOptions);
    services.AddSingleton<IOptions<ControlrViewerOptions>>(_ => Options.Create(viewerOptions));

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
    services.AddSingleton<IStreamMetrics, StreamMetrics>();
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
    services.AddSingleton<IViewerConnectionAuthProvider, ViewerConnectionAuthProvider>();
    services.AddSingleton<IViewerHubConnector, ViewerHubConnector>();

    // Register navigation service.
    services.AddSingleton<INavigationProvider, NavigationProvider>();
    services.AddSingleton<INavigator, Navigator>();

    // Register ViewModels.
    services.AddSingleton<IViewerShellViewModel, ViewerShellViewModel>();
    services.AddSingleton<IFileSystemViewModel, FileSystemViewModel>();
    services.AddSingleton<IRemoteLogsViewModel, RemoteLogsViewModel>();
    services.AddSingleton<IRemoteControlViewModel, RemoteControlViewModel>();
    services.AddSingleton<IRemoteDisplayViewModel, RemoteDisplayViewModel>();
    services.AddSingleton<IChatViewModel, ChatViewModel>();
    services.AddSingleton<TerminalViewModel>();
    services.AddSingleton<ITerminalViewModel>(serviceProvider => serviceProvider.GetRequiredService<TerminalViewModel>());
    services.AddSingleton<ITerminalKeyboardShortcutsDialogViewModel, TerminalKeyboardShortcutsDialogViewModel>();

    // Register Views.
    services.AddSingleton<ViewerShell>();
    services.AddTransient<FileSystemView>();
    services.AddTransient<RemoteLogsView>();
    services.AddTransient<RemoteControlView>();
    services.AddTransient<RemoteDisplayView>();
    services.AddTransient<TerminalView>();
    services.AddTransient<ChatView>();
    services.AddSingleton<TerminalKeyboardShortcutsDialogView>();
    services.AddTransient<ConfirmationDialogView>();
    services.AddTransient<TextPromptDialogView>();
    services.AddTransient<DesktopPreviewDialogView>();

    services.AddControlrSnackbar();
    services.AddControlrDialogs();
    services.AddSingleton<IDesktopPreviewDialogViewModelFactory, DesktopPreviewDialogViewModelFactory>();

    return services.BuildServiceProvider();
  }

  private static void ValidateViewerOptions(ControlrViewerOptions viewerOptions)
  {
    if (viewerOptions.DeviceId == Guid.Empty)
    {
      throw new InvalidOperationException("DeviceId is required.");
    }

    if (viewerOptions.BaseUrl is null)
    {
      throw new InvalidOperationException("BaseUrl is required.");
    }

    if (viewerOptions.AuthenticationMethod == ViewerAuthenticationMethod.PersonalAccessToken &&
        string.IsNullOrWhiteSpace(viewerOptions.PersonalAccessToken))
    {
      throw new InvalidOperationException("A personal access token is required when AuthenticationMethod is PersonalAccessToken.");
    }
  }
}
