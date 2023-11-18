using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services;
using ControlR.Agent.Services.Linux;
using ControlR.Agent.Services.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Devices.Common.Services.Interfaces;
using ControlR.Devices.Common.Services.Linux;
using ControlR.Devices.Common.Services.Windows;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Buffers;
using ControlR.Shared.Services.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Startup;

internal static class IHostBuilderExtensions
{
    internal static IHostBuilder AddControlRAgent(this IHostBuilder builder, StartupMode startupMode)
    {
        if (Environment.UserInteractive)
        {
            builder.UseConsoleLifetime();
        }
        else if (OperatingSystem.IsWindows())
        {
            builder.UseWindowsService(config =>
            {
                config.ServiceName = "ControlR.Agent";
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            builder.UseSystemd();
        }

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var appDir = EnvironmentHelper.Instance.StartupDirectory;
            Guard.IsNotNullOrWhiteSpace(appDir);
            var appSettingsPath = Path.Combine(appDir, "appsettings.json");

            config
                .AddEnvironmentVariables()
                .AddJsonFile(Path.Combine(appSettingsPath), true, true)
                .AddJsonFile(Path.Combine(appDir, $"appsettings.{context.HostingEnvironment.EnvironmentName}.json"), true, true);
        });

        builder.ConfigureServices((context, services) =>
        {
            services
                .AddOptions<AppOptions>()
                .Bind(context.Configuration.GetSection(nameof(AppOptions)));

            services.AddHttpClient<IDownloadsApi, DownloadsApi>();

            services.AddSingleton<ISettingsProvider, SettingsProvider>();
            services.AddSingleton<IProcessManager, ProcessManager>();
            services.AddSingleton<IEnvironmentHelper>(_ => EnvironmentHelper.Instance);
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
            services.AddSingleton<IKeyProvider, KeyProvider>();
            services.AddSingleton(WeakReferenceMessenger.Default);
            services.AddSingleton<ISystemTime, SystemTime>();
            services.AddSingleton<IMemoryProvider, MemoryProvider>();

            if (startupMode == StartupMode.Run)
            {
                services.AddSingleton<IAgentUpdater, AgentUpdater>();
                services.AddSingleton<ILocalProxy, LocalProxy>();
                services.AddSingleton<ICpuUtilizationSampler, CpuUtilizationSampler>();
                services.AddSingleton<IAgentHubConnection, AgentHubConnection>();
                services.AddSingleton<ITerminalStore, TerminalStore>();
                services.AddHostedService(services => services.GetRequiredService<IAgentUpdater>());
                services.AddHostedService(services => services.GetRequiredService<ICpuUtilizationSampler>());
                services.AddHostedService(services => (AgentHubConnection)services.GetRequiredService<IAgentHubConnection>());
                services.AddHostedService<AgentHeartbeatTimer>();
                services.AddHostedService<DtoHandler>();
            }

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
            {
                services.AddSingleton<IDeviceDataGenerator, DeviceDataGeneratorWin>();
                services.AddSingleton<IAgentInstaller, AgentInstallerWindows>();
                services.AddSingleton<IVncSessionLauncher, VncSessionLauncherWindows>();
                services.AddSingleton<IPowerControl, PowerControlWindows>();
                services.AddSingleton<IElevationChecker, ElevationCheckerWin>();
            }
            else if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IDeviceDataGenerator, DeviceDataGeneratorLinux>();
                services.AddSingleton<IAgentInstaller, AgentInstallerLinux>();
                services.AddSingleton<IVncSessionLauncher, VncSessionLauncherLinux>();
                services.AddSingleton<IPowerControl, PowerControlLinux>();
                services.AddSingleton<IElevationChecker, ElevationCheckerLinux>();
            }
            else
            {
                throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
            }
        })
        .ConfigureLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            logging.AddProvider(new FileLoggerProvider(
                version,
                () => LoggingConstants.LogPath,
                TimeSpan.FromDays(7)));
            logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        });

        return builder;
    }
}