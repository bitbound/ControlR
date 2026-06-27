using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services;
using ControlR.Agent.Common.Services.Linux;
using ControlR.Agent.Common.Services.Mac;
using ControlR.Agent.Common.Services.Windows;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ControlR.Web.ServiceDefaults;
using ControlR.Agent.Common.Services.Terminal;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Agent.Common.Services.FileManager;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.Hosting;
using ControlR.Libraries.Serilog;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Agent.Shared.Services;

namespace ControlR.Agent.Common.Startup;

internal static class HostApplicationBuilderExtensions
{
  internal static HostApplicationBuilder AddControlRAgent(
    this HostApplicationBuilder builder,
    StartupMode startupMode,
    string? instanceId,
    Uri? serverUri,
    bool loadAppSettings = true)
  {

    instanceId = instanceId?.SanitizeForFileSystem();
    var services = builder.Services;
    var configuration = builder.Configuration;
    services
      .AddWindowsService(config =>
      {
        config.ServiceName = "ControlR.Agent";
      })
      .AddSystemd();

    // Prevent reading for appsettings in same directory as the executable.
    // We want it to use the custom path, which we set further below.
    if (!SystemEnvironment.Instance.IsDebug)
    {
      configuration.Sources.Clear();
    }

    builder.Configuration
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        { $"{InstanceOptions.SectionKey}:{nameof(InstanceOptions.InstanceId)}", instanceId },
        { $"{AgentAppOptions.SectionKey}:{nameof(AgentAppOptions.ServerUri)}", serverUri?.ToString() },
      })
      .AddEnvironmentVariables();

    services
      .AddOptions<AgentAppOptions>()
      .Bind(configuration.GetSection(AgentAppOptions.SectionKey));

    services
      .AddOptions<DeveloperOptions>()
      .Bind(configuration.GetSection(DeveloperOptions.SectionKey));

    services
      .AddOptions<InstanceOptions>()
      .Bind(configuration.GetSection(InstanceOptions.SectionKey));

    var pathProvider = GetTempPathProvider(builder);

    if (loadAppSettings)
    {
      builder.Configuration.AddJsonFile(pathProvider.GetAgentAppSettingsPath(), true, true);
    }

    var appOptions = builder.Configuration
      .GetSection(AgentAppOptions.SectionKey)
      .Get<AgentAppOptions>() ?? new AgentAppOptions();

    services.AddHttpClient<IDownloadsApi, DownloadsApi>(ConfigureHttpClient);
    services.AddControlrApiClient(options =>
    {
      if (appOptions.ServerUri is null)
      {
        throw new ArgumentException("ServerUri must be provided in configuration or app settings.");
      }
      options.BaseUrl = appOptions.ServerUri;
    });

    services.AddAgentSharedServices();
    services.AddSingleton<IProcessManager, ProcessManager>();
    services.AddSingleton<ISystemEnvironment>(_ => SystemEnvironment.Instance);
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<IFileAccessPermissions, FileAccessPermissions>();
    services.AddSingleton<IFileManager, FileManager>();
    services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
    services.AddSingleton<ILocalSocketProxy, LocalSocketProxy>();
    services.AddSingleton(WeakReferenceMessenger.Default);
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IMemoryProvider, MemoryProvider>();
    services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
    services.AddSingleton<IWaiter, Waiter>();
    services.AddSingleton<IRetryer, Retryer>();
    services.AddSingleton<IEmbeddedResourceAccessor, EmbeddedResourceAccessor>();
    services.AddSingleton<IAgentMaintenanceService, AgentMaintenanceService>();
    services.AddSingleton<IDesktopClientRepairCoordinator, DesktopClientRepairCoordinator>();
    services.AddSingleton<ITerminalSessionFactory, TerminalSessionFactory>();
    services.AddSingleton<ITerminalStore, TerminalStore>();
    services.AddSingleton<IIpcServerStore, IpcServerStore>();
    services.AddSingleton<IIpcClientAuthenticator, IpcClientAuthenticator>();
    services.AddSingleton<IAgentHeartbeatTimer, AgentHeartbeatTimer>();
    services.AddControlrIpcServer<AgentRpcService>();
    services.AddStronglyTypedSignalrClient<IAgentHub, IAgentHubClient, AgentHubClient>(ServiceLifetime.Singleton);

    if (OperatingSystem.IsWindowsVersionAtLeast(8))
    {
      services.AddSingleton<IWin32Interop, Win32Interop>();
      services.AddSingleton<IDesktopSessionProvider, DesktopSessionProviderWindows>();
      services.AddSingleton<IDeviceInfoProvider, DeviceInfoProviderWin>();
      services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSamplerWin>();
      services.AddSingleton<IPowerControl, PowerControlWindows>();
      services.AddSingleton<IIpcClientCredentialsProvider, IpcClientCredentialsProviderWindows>();
      services.AddSingleton<IDesktopClientFileVerifier, DesktopClientFileVerifierWin>();
    }
    else if (OperatingSystem.IsLinux())
    {
      services.AddSingleton<IDeviceInfoProvider, DeviceInfoProviderLinux>();
      services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSampler>();
      services.AddSingleton<IDesktopSessionProvider, DesktopSessionProviderLinux>();
      services.AddSingleton<IPowerControl, PowerControlLinux>();
      services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
      services.AddSingleton<IIpcClientCredentialsProvider, IpcClientCredentialsProviderLinux>();
      services.AddSingleton<IDesktopClientFileVerifier, DesktopClientFileVerifierLinux>();
      services.AddSingleton<IDesktopEnvironmentDetectorAgent, DesktopEnvironmentDetectorAgent>();
    }
    else if (OperatingSystem.IsMacOS())
    {
      services.AddSingleton<IDeviceInfoProvider, DeviceInfoProviderMac>();
      services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSampler>();
      services.AddSingleton<IDesktopSessionProvider, DesktopSessionProviderMac>();
      services.AddSingleton<IPowerControl, PowerControlMac>();
      services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
      services.AddSingleton<IIpcClientCredentialsProvider, IpcClientCredentialsProviderMac>();
      services.AddSingleton<IDesktopClientFileVerifier, DesktopClientFileVerifierMac>();
    }
    else
    {
      throw new PlatformNotSupportedException();
    }

    // Add services only needed when running.
    if (startupMode == StartupMode.Run)
    {
      services.AddHostedService<DotnetExtractDirectoryCleanupHostedService>();
      services.AddHostedService(s => s.GetRequiredService<IAgentMaintenanceService>());
      services.AddHostedService<IpcServerWatcher>();
      services.AddHostedService<HubConnectionInitializer>();
      services.AddHostedService(x => x.GetRequiredService<IAgentHeartbeatTimer>());
      services.AddHostedService<MessageHandler>();
      services.AddHostedService<HostLifetimeEventResponder>();
      services.AddHostedService(s => s.GetRequiredService<ICpuUtilizationSampler>());
      services.AddHostedService<FilePermissionsEnforcer>();

      if (OperatingSystem.IsWindowsVersionAtLeast(8))
      {
        services.AddSingleton<IDesktopClientLaunchTracker, DesktopClientLaunchTracker>();
        services.AddHostedService<DesktopClientWatcherWin>();
        services.AddHostedService<IpcServerInitializerWindows>();
      }
      else if (OperatingSystem.IsMacOS())
      {
        services.AddHostedService<DesktopClientWatcherMac>();
        services.AddHostedService<IpcServerInitializerMac>();
      }
      else if (OperatingSystem.IsLinux())
      {
        services.AddHostedService<DesktopClientWatcherLinux>();
        services.AddHostedService<IpcServerInitializerLinux>();
      }
    }

    builder.AddServiceDefaults(ServiceNames.ControlrAgent);
    builder.BootstrapSerilog(pathProvider.GetAgentLogFilePath(), TimeSpan.FromDays(7));

    return builder;
  }

  private static void ConfigureHttpClient(IServiceProvider provider, HttpClient client)
  {
    var options = provider.GetRequiredService<IOptionsMonitor<AgentAppOptions>>();
    client.BaseAddress = options.CurrentValue.ServerUri;
  }

  private static FileSystemPathProvider GetTempPathProvider(HostApplicationBuilder builder)
  {
    var instanceOptions = builder.Configuration
      .GetSection(InstanceOptions.SectionKey)
      .Get<InstanceOptions>() ?? new InstanceOptions();

    IElevationChecker elevationChecker =
      SystemEnvironment.Instance.IsWindows()
        ? new ElevationCheckerWin()
        : SystemEnvironment.Instance.IsMacOS()
          ? new ElevationCheckerMac()
          : SystemEnvironment.Instance.IsLinux()
            ? new ElevationCheckerLinux()
            : throw new PlatformNotSupportedException();

    return new FileSystemPathProvider(
      SystemEnvironment.Instance,
      elevationChecker,
      new FileSystem(new SerilogLogger<FileSystem>()),
      new OptionsMonitorWrapper<InstanceOptions>(instanceOptions));
  }
}
