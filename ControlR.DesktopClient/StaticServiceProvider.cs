using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.ViewModels;
using ControlR.DesktopClient.Views;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
// ReSharper disable UnusedMethodReturnValue.Local

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

  private static IServiceCollection AddControlrDesktop(
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
    services.AddControlrIpc()
      .AddSingleton(TimeProvider.System)
      .AddSingleton(WeakReferenceMessenger.Default)
      .AddSingleton<IProcessManager, ProcessManager>()
      .AddSingleton<IFileSystem, FileSystem>()
      .AddSingleton<ISystemEnvironment, SystemEnvironment>()
      .AddSingleton<INavigationProvider, NavigationProvider>()
      .AddSingleton<IMainWindowProvider, MainWindowProvider>()
      .AddSingleton<IThemeProvider, ThemeProvider>()
      .AddSingleton<IAppViewModel, AppViewModel>()
      .AddSingleton<IMainWindowViewModel, MainWindowViewModel>()
      .AddSingleton<IRemoteControlHostManager, RemoteControlHostManager>()
      .AddSingleton<IDialogProvider, DialogProvider>()
      .AddSingleton<IUserInteractionService, UserInteractionService>()
      .AddSingleton<IDesktopPreviewProvider, DesktopPreviewProvider>()
      .AddSingleton<IChatSessionManager, ChatSessionManager>()
      .AddSingleton<IIpcClientAccessor, IpcClientAccessor>()
      .AddSingleton<IManagedDeviceViewModel, ManagedDeviceViewModel>()
      .AddSingleton<IToaster, Toaster>()
      .AddSingleton<IImageUtility, ImageUtility>()
      .AddTransient<MainWindow>()
      .AddTransient<IMessageBoxViewModel, MessageBoxViewModel>()
      .AddTransient<ManagedDeviceView>()
      .AddTransient<ChatWindow>()
      .AddTransient<IChatWindowViewModel, ChatWindowViewModel>()
      .AddTransient<ToastWindow>()
      .AddTransient<IToastWindowViewModel, ToastWindowViewModel>()
      .AddHostedService<IpcClientManager>()
      // Cross-platform Avalonia-based toaster
      .AddSingleton<IToaster, Toaster>();


#if WINDOWS_BUILD
    services.AddSingleton<IScreenGrabber, ScreenGrabberWindows>()
      .AddSingleton<IWin32Interop, Win32Interop>()
      .AddSingleton<IDxOutputDuplicator, DxOutputDuplicator>()
      .AddSingleton<IDisplayManager, DisplayManagerWindows>();
#endif

#if UNIX_BUILD
    services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
#endif

#if LINUX_BUILD
    services.AddSingleton<IDesktopEnvironmentDetector, DesktopEnvironmentDetector>();

    var desktopEnvironment = DesktopEnvironmentDetector.Instance.GetDesktopEnvironment();

    switch (desktopEnvironment)
    {
      case DesktopEnvironmentType.Wayland:
        services
          .AddSingleton<IWaylandPermissionProvider, WaylandPermissionProvider>()
          .AddSingleton<IScreenGrabber, ScreenGrabberWayland>()
          .AddSingleton<IDisplayManager, DisplayManagerWayland>()
          .AddSingleton<IWaylandPortalAccessor, WaylandPortalAccessor>()
          .AddSingleton<IPipeWireStreamFactory, PipeWireStreamFactory>()
          .AddHostedService<RemoteControlPermissionMonitor>();
        break;
      case DesktopEnvironmentType.X11:
        services
          .AddSingleton<IScreenGrabber, ScreenGrabberX11>()
          .AddSingleton<IDisplayManager, DisplayManagerX11>();
        break;
      default:
        throw new NotSupportedException("Unsupported desktop environment detected.");
    }
#endif

#if MAC_BUILD
    services
      .AddHostedService<RemoteControlPermissionMonitor>()
      .AddSingleton<IScreenGrabber, ScreenGrabberMac>()
      .AddSingleton<IMacInterop, MacInterop>()
      .AddSingleton<IDisplayManager, DisplayManagerMac>();
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

#if WINDOWS_BUILD
    var logsPath = PathConstants.GetLogsPath(instanceId);
#elif MAC_BUILD
    var logsPath = Mac.PathConstants.GetLogsPath(instanceId);
#elif LINUX_BUILD
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