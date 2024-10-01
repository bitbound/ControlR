using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Options;
using ControlR.Agent.Services;
using ControlR.Agent.Services.Fakes;
using ControlR.Agent.Services.Linux;
using ControlR.Agent.Services.Mac;
using ControlR.Agent.Services.Windows;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.DevicesNative.Services;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Signalr.Client;
using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SimpleIpc;

namespace ControlR.Agent.Startup;

internal static class HostApplicationBuilderExtensions
{
  internal static IHostApplicationBuilder AddControlRAgent(this HostApplicationBuilder builder, StartupMode startupMode, string? instanceId)
  {
    var services = builder.Services;
    var configuration = builder.Configuration;
    var logging = builder.Logging;

    services
      .AddWindowsService(config =>
      {
        config.ServiceName = "ControlR.Agent";
      })
      .AddSystemd();
    configuration.Sources.Clear();

    if (!EnvironmentHelper.Instance.IsDebug)
    {
      configuration.Sources.Clear();
    }

    builder.Configuration
      .AddJsonFile(PathConstants.GetAppSettingsPath(instanceId), true, true)
      .AddEnvironmentVariables()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        { "InstanceOptions:InstanceId", instanceId }
      });

    services
      .AddOptions<AgentAppOptions>()
      .Bind(configuration.GetSection(AgentAppOptions.SectionKey));

    services
      .AddOptions<InstanceOptions>()
      .Bind(configuration.GetSection(InstanceOptions.SectionKey));

    services.AddHttpClient<IDownloadsApi, DownloadsApi>(ConfigureHttpClient);
    services.AddHttpClient<IVersionApi, VersionApi>(ConfigureHttpClient);
    services.AddHttpClient<IReleasesApi, ReleasesApi>();

    services.AddSingleton<ISettingsProvider, SettingsProvider>();
    services.AddSingleton<IProcessManager, ProcessManager>();
    services.AddSingleton<IEnvironmentHelper>(_ => EnvironmentHelper.Instance);
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
    services.AddSingleton<IStreamingSessionCache, StreamingSessionCache>();
    services.AddSingleton(WeakReferenceMessenger.Default);
    services.AddSingleton<ISystemTime, SystemTime>();
    services.AddSingleton<IMemoryProvider, MemoryProvider>();
    services.AddSingleton<IRegistryAccessor, RegistryAccessor>();
    services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
    services.AddSingleton<IDelayer, Delayer>();
    services.AddSingleton<IRetryer, Retryer>();
    services.AddSimpleIpc();
    services.AddHostedService<HostLifetimeEventResponder>();

    if (startupMode == StartupMode.Run)
    {
      services.AddSingleton<IAgentUpdater, AgentUpdater>();
      services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSampler>();
      services.AddSingleton<ITerminalStore, TerminalStore>();
      services.AddSingleton<IStreamingSessionCache, StreamingSessionCache>();
      services.AddStronglyTypedSignalrClient<IAgentHub, IAgentHubClient, AgentHubClient>();
      services.AddSingleton<IAgentHubConnection, AgentHubConnection>();
      services.AddHostedService(services => services.GetRequiredService<IAgentUpdater>());
      services.AddHostedService(services => services.GetRequiredService<ICpuUtilizationSampler>());
      services.AddHostedService(services => services.GetRequiredService<IAgentHubConnection>());
      services.AddHostedService<AgentHeartbeatTimer>();
      services.AddHostedService(services => services.GetRequiredService<IStreamerUpdater>());
      services.AddHostedService<DtoHandler>();

      if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
      {
        services.AddSingleton<IStreamerLauncher, StreamerLauncherWindows>();
        services.AddSingleton<IStreamerUpdater, StreamerUpdaterWindows>();
        services.AddHostedService<StreamingSessionWatcher>();
      }
      else if (OperatingSystem.IsLinux())
      {
        services.AddSingleton<IStreamerUpdater, StreamerUpdaterFake>();
        services.AddSingleton<IStreamerLauncher, StreamerLauncherFake>();
      }
      else if (OperatingSystem.IsMacOS())
      {
        services.AddSingleton<IStreamerUpdater, StreamerUpdaterFake>();
        services.AddSingleton<IStreamerLauncher, StreamerLauncherFake>();
      }
      else
      {
        throw new PlatformNotSupportedException();
      }
    }

    if (startupMode == StartupMode.EchoDesktop)
    {
      services.AddSingleton<IDesktopEchoer, DesktopEchoer>();
    }

    if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
    {
      services.AddSingleton<IWin32Interop, Win32Interop>();
      services.AddSingleton<IDeviceDataGenerator, DeviceDataGeneratorWin>();
      services.AddSingleton<IAgentInstaller, AgentInstallerWindows>();
      services.AddSingleton<IPowerControl, PowerControlWindows>();
      services.AddSingleton<IElevationChecker, ElevationCheckerWin>();
    }
    else if (OperatingSystem.IsLinux())
    {
      services.AddSingleton<IDeviceDataGenerator, DeviceDataGeneratorLinux>();
      services.AddSingleton<IAgentInstaller, AgentInstallerLinux>();
      services.AddSingleton<IPowerControl, PowerControlMac>();
      services.AddSingleton<IElevationChecker, ElevationCheckerLinux>();
      services.AddSingleton<IWin32Interop, Win32InteropFake>();
    }
    else if (OperatingSystem.IsMacOS())
    {
      services.AddSingleton<IDeviceDataGenerator, DeviceDataGeneratorMac>();
      services.AddSingleton<IAgentInstaller, AgentInstallerMac>();
      services.AddSingleton<IPowerControl, PowerControlMac>();
      services.AddSingleton<IElevationChecker, ElevationCheckerMac>();
      services.AddSingleton<IWin32Interop, Win32InteropFake>();
    }
    else
    {
      throw new PlatformNotSupportedException();
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