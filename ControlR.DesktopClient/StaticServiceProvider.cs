using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace ControlR.DesktopClient;

internal static class StaticServiceProvider
{
  private static ServiceProvider? _designTimeProvider;
  private static ServiceProvider? _provider;

  public static IServiceProvider Instance => _provider ?? GetDesignTimeProvider();

  public static void Build(
    IControlledApplicationLifetime lifetime,
    string? instanceId)
  {
    if (_provider is not null)
    {
      return;
    }
    var services = new ServiceCollection();
    services.AddSingleton(lifetime);
    services.AddControlrDesktop(instanceId);
    _provider = services.BuildServiceProvider();
  }

  internal static IServiceCollection AddControlrDesktop(
    this IServiceCollection services,
    string? instanceId = null)
  {
    var configuration = new ConfigurationBuilder()
      .AddEnvironmentVariables()
      .Build();

    services.AddSingleton<IConfiguration>(configuration);

    // Logging
    services.AddLogging(builder =>
    {
      builder
        .AddConsole()
        .AddDebug();
    });

    services.AddSerilog(instanceId, configuration);

    // Options
    services.AddOptions();

    services.Configure<DesktopClientOptions>(options =>
    {
      options.InstanceId = instanceId;
    });

    // Services
    services
      .AddControlrIpcClient<DesktopClientRpcService>()
      .AddSingleton(TimeProvider.System)
      .AddSingleton<IMessenger>(new WeakReferenceMessenger())
      .AddSingleton<IProcessManager, ProcessManager>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<ISystemEnvironment, SystemEnvironment>()
      .AddSingleton<INavigationProvider, NavigationProvider>()
      .AddSingleton<IMainWindowProvider, MainWindowProvider>()
      .AddSingleton<IThemeProvider, ThemeProvider>()
      .AddSingleton<IAppViewModel, AppViewModel>()
      .AddSingleton<IMainWindowViewModel, MainWindowViewModel>()
      .AddSingleton<IConnectionsViewModel, ConnectionsViewModel>()
      .AddSingleton<ISettingsViewModel, SettingsViewModel>()
      .AddSingleton<IAboutViewModel, AboutViewModel>()
      .AddSingleton<IRemoteControlHostManager, RemoteControlHostManager>()
      .AddSingleton<IDialogProvider, DialogProvider>()
      .AddSingleton<IUserInteractionService, UserInteractionService>()
      .AddSingleton<IDesktopPreviewProvider, DesktopPreviewProvider>()
      .AddSingleton<IChatSessionManager, ChatSessionManager>()
      .AddSingleton<IpcClientManager>()
      .AddSingleton<IIpcClientAccessor>(sp => sp.GetRequiredService<IpcClientManager>())
      .AddSingleton<IToaster, Toaster>()
      .AddSingleton<IUiThread, UiThread>()
      .AddSingleton<IImageUtility, ImageUtility>()
      .AddSingleton<IAppLifetimeNotifier, AppLifetimeNotifier>()
      .AddSingleton<IViewModelFactory, ViewModelFactory>()
      .AddTransient<MainWindow>()
      .AddTransient<ConnectionsView>()
      .AddTransient<SettingsView>()
      .AddTransient<AboutView>()
      .AddTransient<IMessageBoxViewModel, MessageBoxViewModel>()
      .AddTransient<ChatWindow>()
      .AddTransient<IChatWindowViewModel, ChatWindowViewModel>()
      .AddTransient<ToastWindow>()
      .AddTransient<IToastWindowViewModel, ToastWindowViewModel>()
      .AddHostedService(sp => sp.GetRequiredService<IpcClientManager>())
      .AddHostedService(sp => sp.GetRequiredService<IAppLifetimeNotifier>());


#if IS_WINDOWS
      services
        .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberWindows>>()
        .AddSingleton(services => services.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
        .AddSingleton<IWin32Interop, Win32Interop>()
        .AddSingleton<IDxOutputDuplicator, DxOutputDuplicator>()
        .AddSingleton<IWindowsMessagePump, WindowsMessagePump>()
        .AddSingleton<IPermissionsViewModel, PermissionsViewModel>()
        .AddSingleton<DisplayManagerWindows>()
        .AddSingleton<IDisplayManager>(s => s.GetRequiredService<DisplayManagerWindows>())
        .AddSingleton<IDisplayManagerWindows>(s => s.GetRequiredService<DisplayManagerWindows>())
        .AddTransient<PermissionsView>();
#endif

#if IS_MACOS
      services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
      services
        .AddHostedService<RemoteControlPermissionMonitorMac>()
        .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberMac>>()
        .AddSingleton(services => services.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
        .AddSingleton<IMacInterop, MacInterop>()
        .AddSingleton<IDisplayEnumHelperMac, DisplayEnumHelperMac>()
        .AddSingleton<IDisplayManager, DisplayManagerMac>()
        .AddSingleton<IPermissionsViewModelMac, PermissionsViewModelMac>()
        .AddTransient<PermissionsViewMac>();
#endif

#if IS_LINUX
      services
        .AddSingleton<IFileSystemUnix, FileSystemUnix>()
        .AddSingleton<IDesktopEnvironmentDetector, DesktopEnvironmentDetector>();

      var desktopEnvironment = DesktopEnvironmentDetector.Instance.GetDesktopEnvironment();

      switch (desktopEnvironment)
      {
        case DesktopEnvironmentType.Wayland:
          services
            .AddHostedService<RemoteControlPermissionMonitorWayland>()
            .AddSingleton<IWaylandPermissionProvider, WaylandPermissionProvider>()
            .AddSingleton<IXdgDesktopPortalFactory, XdgDesktopPortalFactory>()
            .AddSingleton(services => services.GetRequiredService<IXdgDesktopPortalFactory>().GetOrCreateDefault())
            .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberWayland>>()
            .AddSingleton(services => services.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
            .AddSingleton<DisplayManagerWayland>()
            .AddSingleton<IDisplayManager>(services => services.GetRequiredService<DisplayManagerWayland>())
            .AddSingleton<IDisplayManagerWayland>(services => services.GetRequiredService<DisplayManagerWayland>())
            .AddSingleton<IPipeWireStreamFactory, PipeWireStreamFactory>()
            .AddSingleton<IPermissionsViewModelWayland, PermissionsViewModelWayland>()
            .AddTransient<PermissionsViewWayland>();
          break;
        case DesktopEnvironmentType.X11:
          services
            .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberX11>>()
            .AddSingleton(services => services.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
            .AddSingleton<IDisplayManager, DisplayManagerX11>()
            .AddSingleton<IPermissionsViewModel, PermissionsViewModel>()
            .AddTransient<PermissionsView>();
          break;
        default:
          throw new NotSupportedException("Unsupported desktop environment detected.");
      }
#endif

    return services;
  }

  private static IServiceCollection AddSerilog(
    this IServiceCollection services,
    string? instanceId,
    IConfigurationRoot configuration)
  {
    if (Design.IsDesignMode)
    {
      return services;
    }

#if IS_WINDOWS
    var logsPath = PathConstants.GetLogsPath(instanceId);
#elif IS_MACOS
    var logsPath = Mac.PathConstants.GetLogsPath(instanceId);
#elif IS_LINUX
    var logsPath = Linux.PathConstants.GetLogsPath(instanceId);
#else
    throw new PlatformNotSupportedException("Unsupported operating system.");
#endif

    services.BootstrapSerilog(
      configuration,
      logsPath,
      TimeSpan.FromDays(7),
      config =>
      {
        if (SystemEnvironment.Instance.IsDebug)
        {
          config.MinimumLevel.Debug();
        }
      });

    return services;
  }

  private static ServiceProvider GetDesignTimeProvider()
  {
    if (_designTimeProvider is not null)
    {
      return _designTimeProvider;
    }

    var services = new ServiceCollection();
    services.AddControlrDesktop();
    _designTimeProvider = services.BuildServiceProvider();
    return _designTimeProvider;
  }
}
