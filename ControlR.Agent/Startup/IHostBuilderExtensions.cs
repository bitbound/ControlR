﻿using Bitbound.SimpleMessenger;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services;
using ControlR.Agent.Services.Linux;
using ControlR.Agent.Services.Mac;
using ControlR.Agent.Services.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Devices.Common.Services.Interfaces;
using ControlR.Devices.Common.Services.Linux;
using ControlR.Devices.Common.Services.Mac;
using ControlR.Devices.Common.Services.Windows;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Buffers;
using ControlR.Shared.Services.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
            var startupDir = EnvironmentHelper.Instance.StartupDirectory;

            if (!EnvironmentHelper.Instance.IsDebug)
            {
                config.Sources.Clear();
            }

            config
                .AddJsonFile(SettingsProvider.AppSettingsPath, true, true)
                .AddEnvironmentVariables();
        });

        builder.ConfigureServices((context, services) =>
        {
            services
                .AddOptions<AgentAppOptions>()
                .Bind(context.Configuration.GetSection(AgentAppOptions.ConfigurationKey));

            services.AddHttpClient<IDownloadsApi, DownloadsApi>(ConfigureHttpClient);
            services.AddHttpClient<IVersionApi, VersionApi>(ConfigureHttpClient);

            services.AddSingleton<ISettingsProvider, SettingsProvider>();
            services.AddSingleton<IProcessManager, ProcessManager>();
            services.AddSingleton<IEnvironmentHelper>(_ => EnvironmentHelper.Instance);
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
            services.AddSingleton<IKeyProvider, KeyProvider>();
            services.AddSingleton(WeakReferenceMessenger.Default);
            services.AddSingleton<ISystemTime, SystemTime>();
            services.AddSingleton<IMemoryProvider, MemoryProvider>();
            services.AddSingleton<IRegistryAccessor, RegistryAccessor>();
            services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
            services.AddSingleton<IDelayer, Delayer>();
            services.AddSingleton<IRetryer, Retryer>();

            if (startupMode == StartupMode.Run)
            {
                services.AddSingleton<IAgentUpdater, AgentUpdater>();
                services.AddSingleton<ILocalProxyAgent, LocalProxyAgent>();
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
                services.AddSingleton<IPowerControl, PowerControlMac>();
                services.AddSingleton<IElevationChecker, ElevationCheckerLinux>();
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IDeviceDataGenerator, DeviceDataGeneratorMac>();
                services.AddSingleton<IAgentInstaller, AgentInstallerMac>();
                services.AddSingleton<IVncSessionLauncher, VncSessionLauncherMac>();
                services.AddSingleton<IPowerControl, PowerControlMac>();
                services.AddSingleton<IElevationChecker, ElevationCheckerMac>();
            }
            else
            {
                throw new PlatformNotSupportedException();
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

    private static void ConfigureHttpClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptionsMonitor<AgentAppOptions>>();
        if (Uri.TryCreate(options.CurrentValue.ServerUri, UriKind.Absolute, out var serverUri))
        {
            client.BaseAddress = serverUri;
        }
    }
}