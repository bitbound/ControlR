using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services;
using ControlR.Agent.Common.Services.Linux;
using ControlR.Agent.Common.Services.Mac;
using ControlR.Agent.Common.Services.Windows;
using ControlR.Libraries.DevicesCommon.Extensions;
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
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Agent.Common.Services.FileManager;
using ControlR.Libraries.Shared.Hubs.Clients;

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
    builder.AddServiceDefaults(ServiceNames.ControlrAgent);

    instanceId = instanceId?.SanitizeForFileSystem();
    var services = builder.Services;
    var configuration = builder.Configuration;
    services
      .AddWindowsService(config =>
      {
        config.ServiceName = "ControlR.Agent";
      })
      .AddSystemd();

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

    if (loadAppSettings)
    {
      builder.Configuration.AddJsonFile(PathConstants.GetAppSettingsPath(instanceId), true, true);
    }

    services
      .AddOptions<AgentAppOptions>()
      .Bind(configuration.GetSection(AgentAppOptions.SectionKey));

    services
      .AddOptions<DeveloperOptions>()
      .Bind(configuration.GetSection(DeveloperOptions.SectionKey));

    services
      .AddOptions<InstanceOptions>()
      .Bind(configuration.GetSection(InstanceOptions.SectionKey));

    services.AddHttpClient<IDownloadsApi, DownloadsApi>(ConfigureHttpClient);
    services.AddHttpClient<IControlrApi, ControlrApi>(ConfigureHttpClient);

    services.AddSingleton<ISettingsProvider, SettingsProvider>();
    services.AddSingleton<IProcessManager, ProcessManager>();
    services.AddSingleton<ISystemEnvironment>(_ => SystemEnvironment.Instance);
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<IControlrMutationLock, ControlrMutationLock>();
    services.AddSingleton<IFileManager, FileManager>();
    services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
    services.AddSingleton<ILocalSocketProxy, LocalSocketProxy>();
    services.AddSingleton(WeakReferenceMessenger.Default);
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IMemoryProvider, MemoryProvider>();
    services.AddSingleton<IRegistryAccessor, RegistryAccessor>();
    services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
    services.AddSingleton<IWaiter, Waiter>();
    services.AddSingleton<IRetryer, Retryer>();
    services.AddSingleton<IEmbeddedResourceAccessor, EmbeddedResourceAccessor>();
    services.AddSingleton<IEmbeddedDesktopClientProvider, EmbeddedDesktopClientProvider>();
    services.AddSingleton<IAgentUpdater, AgentUpdater>();
    services.AddSingleton<ITerminalSessionFactory, TerminalSessionFactory>();
    services.AddSingleton<ITerminalStore, TerminalStore>();
    services.AddSingleton<IIpcServerStore, IpcServerStore>();
    services.AddSingleton<IIpcClientAuthenticator, IpcClientAuthenticator>();
    services.AddSingleton<IDesktopClientUpdater, DesktopClientUpdater>();
    services.AddSingleton<IAgentHeartbeatTimer, AgentHeartbeatTimer>();
    services.AddControlrIpcServer<AgentRpcService>();
    services.AddStronglyTypedSignalrClient<IAgentHub, IAgentHubClient, AgentHubClient>(ServiceLifetime.Singleton);

    if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
    {
      services.AddSingleton<IWin32Interop, Win32Interop>();
      services.AddSingleton<IDesktopSessionProvider, DesktopSessionProviderWindows>();
      services.AddSingleton<IDeviceDataGenerator, DeviceInfoProviderWin>();
      services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSamplerWin>();
      services.AddSingleton<IAgentInstaller, AgentInstallerWindows>();
      services.AddSingleton<IServiceControl, ServiceControlWindows>();
      services.AddSingleton<IPowerControl, PowerControlWindows>();
      services.AddSingleton<IElevationChecker, ElevationCheckerWin>();
      services.AddSingleton<IClientCredentialsProvider, ClientCredentialsProviderWindows>();
      services.AddSingleton<IDesktopClientFileVerifier, DesktopClientFileVerifierWin>();
    }
    else if (OperatingSystem.IsLinux())
    {
      services.AddSingleton<IDeviceDataGenerator, DeviceInfoProviderLinux>();
      services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSampler>();
      services.AddSingleton<IDesktopSessionProvider, DesktopSessionProviderLinux>();
      services.AddSingleton<IAgentInstaller, AgentInstallerLinux>();
      services.AddSingleton<IServiceControl, ServiceControlLinux>();
      services.AddSingleton<IHeadlessServerDetector, HeadlessServerDetector>();
      services.AddSingleton<ILoggedInUserProvider, LoggedInUserProviderLinux>();
      services.AddSingleton<IPowerControl, PowerControlMac>();
      services.AddSingleton<IElevationChecker, ElevationCheckerLinux>();
      services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
      services.AddSingleton<IClientCredentialsProvider, ClientCredentialsProviderLinux>();
      services.AddSingleton<IDesktopClientFileVerifier, DesktopClientFileVerifierLinux>();
      services.AddSingleton<IDesktopEnvironmentDetectorAgent, DesktopEnvironmentDetectorAgent>();
    }
    else if (OperatingSystem.IsMacOS())
    {
      services.AddSingleton<IDeviceDataGenerator, DeviceInfoProviderMac>();
      services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSampler>();
      services.AddSingleton<IDesktopSessionProvider, DesktopSessionProviderMac>();
      services.AddSingleton<IAgentInstaller, AgentInstallerMac>();
      services.AddSingleton<IServiceControl, ServiceControlMac>();
      services.AddSingleton<IPowerControl, PowerControlMac>();
      services.AddSingleton<IElevationChecker, ElevationCheckerMac>();
      services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
      services.AddSingleton<IClientCredentialsProvider, ClientCredentialsProviderMac>();
      services.AddSingleton<IDesktopClientFileVerifier, DesktopClientFileVerifierMac>();
    }
    else
    {
      throw new PlatformNotSupportedException();
    }

    // Add services only needed when running.
    if (startupMode == StartupMode.Run)
    {
      services.AddHostedService(s => s.GetRequiredService<IAgentUpdater>());
      services.AddHostedService<IpcServerWatcher>();
      services.AddHostedService<HubConnectionInitializer>();
      services.AddHostedService(x => x.GetRequiredService<IAgentHeartbeatTimer>());
      services.AddHostedService(s => s.GetRequiredService<IDesktopClientUpdater>());
      services.AddHostedService<MessageHandler>();
      services.AddHostedService<HostLifetimeEventResponder>();
      services.AddHostedService(s => s.GetRequiredService<ICpuUtilizationSampler>());

      if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
      {
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

    builder.BootstrapSerilog(PathConstants.GetLogsPath(instanceId), TimeSpan.FromDays(7));

    return builder;
  }

  private static void ConfigureHttpClient(IServiceProvider provider, HttpClient client)
  {
    var options = provider.GetRequiredService<IOptionsMonitor<AgentAppOptions>>();
    client.BaseAddress = options.CurrentValue.ServerUri;
  }
}