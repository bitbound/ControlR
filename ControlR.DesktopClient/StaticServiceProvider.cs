using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.ViewModels;
using ControlR.DesktopClient.Views;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.DevicesCommon.Options;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Extensions;
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
    // Confiuguration
    var instanceOptionsData = new Dictionary<string, string?>();
    if (!string.IsNullOrEmpty(instanceId))
    {
      instanceOptionsData["InstanceOptions:InstanceId"] = instanceId.SanitizeForFileSystem();
    }

    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(instanceOptionsData)
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

    services.Configure<InstanceOptions>(
      configuration.GetSection(InstanceOptions.SectionKey));

    // Services
    services.AddControlrIpc();
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IProcessManager, ProcessManager>();
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<INavigationProvider, NavigationProvider>();
    services.AddSingleton<IAppViewModel, AppViewModel>();
    services.AddSingleton<IMainWindowViewModel, MainWindowViewModel>();
    services.AddSingleton<MainWindow>();
    services.AddSingleton<IRemoteControlHostManager, RemoteControlHostManager>();
    services.AddSingleton<IDialogProvider, DialogProvider>();
    services.AddSingleton<IChatSessionManager, ChatSessionManager>();
    services.AddSingleton<IpcClientAccessor>();
    services.AddSingleton<IIpcClientAccessor>(provider => provider.GetRequiredService<IpcClientAccessor>());
    services.AddSingleton<IManagedDeviceViewModel, ManagedDeviceViewModel>();
    services.AddSingleton<IToaster, Toaster>();
    services.AddSingleton<IImageUtility, ImageUtility>();
    services.AddTransient<IMessageBoxViewModel, MessageBoxViewModel>();
    services.AddTransient<ManagedDeviceView>();
    services.AddTransient<ChatWindow>();
    services.AddTransient<IChatWindowViewModel, ChatWindowViewModel>();
    services.AddTransient<ToastWindow>();
    services.AddTransient<IToastWindowViewModel, ToastWindowViewModel>();
    services.AddHostedService<IpcClientManager>();
    
    // Cross-platform Avalonia-based toaster
    services.AddSingleton<IToaster, Toaster>();

#if WINDOWS_BUILD
    services.AddSingleton<IScreenGrabber, ScreenGrabberWindows>();
    services.AddSingleton<IWin32Interop, Win32Interop>();
    services.AddSingleton<IDxOutputGenerator, DxOutputGenerator>();
#endif

#if UNIX_BUILD
    services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
#endif

#if LINUX_BUILD
    services.AddSingleton<IScreenGrabber, ScreenGrabberLinux>();
#endif

#if MAC_BUILD
    services.AddHostedService<PermissionsInitializerMac>();
    services.AddSingleton<IScreenGrabber, ScreenGrabberMac>();
    services.AddSingleton<IMacInterop, MacInterop>();
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

    var logsPath = string.Empty;

#if WINDOWS_BUILD
    logsPath = Windows.PathConstants.GetLogsPath(instanceId);
#elif MAC_BUILD
    logsPath = Mac.PathConstants.GetLogsPath(instanceId);
#elif LINUX_BUILD
    logsPath = Linux.PathConstants.GetLogsPath(instanceId);
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