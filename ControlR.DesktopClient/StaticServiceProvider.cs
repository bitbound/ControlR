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
    IClassicDesktopStyleApplicationLifetime lifetime,
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
      .AddSingleton<IAppViewModel, AppViewModel>()
      .AddSingleton<IMainWindowViewModel, MainWindowViewModel>()
      .AddSingleton<IRemoteControlHostManager, RemoteControlHostManager>()
      .AddSingleton<IDialogProvider, DialogProvider>()
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
    services.AddSingleton<IScreenGrabber, ScreenGrabberWindows>();
    services.AddSingleton<IWin32Interop, Win32Interop>();
    services.AddSingleton<IDxOutputDuplicator, DxOutputDuplicator>();
    services.AddSingleton<IDisplayManager, DisplayManagerWindows>();
#endif

#if UNIX_BUILD
    services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
#endif

#if LINUX_BUILD
    services.AddSingleton<IScreenGrabber, ScreenGrabberX11>();
    services.AddSingleton<IDisplayManager, DisplayManagerX11>();
#endif

#if MAC_BUILD
    services.AddHostedService<PermissionsInitializerMac>();
    services.AddSingleton<IScreenGrabber, ScreenGrabberMac>();
    services.AddSingleton<IMacInterop, MacInterop>();
    services.AddSingleton<IDisplayManager, DisplayManagerMac>();
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