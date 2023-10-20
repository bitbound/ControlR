using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services;
using ControlR.Agent.Services.Windows;
using ControlR.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.Reflection;

namespace ControlR.Agent.Startup;
internal class CommandProvider
{
    internal static Command GetInstallCommand(string[] args)
    {
        var authorizedKeyOption = new Option<string>(
            new[] { "-a", "--authorized-key" },
            "An optional public key to preconfigure with authorization to this device.");

        var installCommand = new Command("install", "Install the ControlR service.")
        {
            authorizedKeyOption
        };

        installCommand.SetHandler(async (authorizedKey) =>
        {
            var host = CreateHost(StartupMode.Install, args);
            var installer = host.Services.GetRequiredService<IAgentInstaller>();
            await installer.Install(authorizedKey);
            await host.RunAsync();
        }, authorizedKeyOption);

        return installCommand;

    }
    internal static Command GetUninstallCommand(string[] args)
    {
        var unInstallCommand = new Command("uninstall", "Uninstall the ControlR service.");
        unInstallCommand.SetHandler(async () =>
        {
            var host = CreateHost(StartupMode.Uninstall, args);
            var installer = host.Services.GetRequiredService<IAgentInstaller>();
            await installer.Uninstall();
            await host.RunAsync();
        });
        return unInstallCommand;
    }

    internal static Command GetRunCommand(string[] args)
    {
        var runCommand = new Command("run", "Run the ControlR service.");

        runCommand.SetHandler(async () =>
        {
            var host = CreateHost(StartupMode.Run, args);

            var appDir = EnvironmentHelper.Instance.StartupDirectory;
            var appSettingsPath = Path.Combine(appDir!, "appsettings.json");

            if (!File.Exists(appSettingsPath))
            {
                using var mrs = Assembly.GetExecutingAssembly().GetManifestResourceStream("ControlR.Agent.appsettings.json");
                if (mrs is not null)
                {
                    using var fs = new FileStream(appSettingsPath, FileMode.Create);
                    await mrs.CopyToAsync(fs);
                }
            }

            var hubConnection = host.Services.GetRequiredService<IAgentHubConnection>();
            await hubConnection.Start();
            await host.RunAsync();
        });

        return runCommand;
    }

    internal static Command GetSidecarCommand(string[] args)
    {
        var agentPipeOption = new Option<string>(
            new[] { "-a", "--agent-pipe" },
            "The agent pipe name to which the watcher should connect.")
        {
            IsRequired = true
        };

        var parentIdOption = new Option<int>(
            new[] { "-p", "--parent-id" },
            "The calling process's ID.")
        {
            IsRequired = true
        };

        var sidecarCommand = new Command("sidecar", "Watches for desktop changes (winlogon/UAC) for the streamer process.")
        {
            agentPipeOption,
            parentIdOption
        };

        sidecarCommand.SetHandler(async (agentPipeName, parentProcessId) =>
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("This command is only available on Windows.");
                return;
            }
            var host = CreateHost(StartupMode.Sidecar, args);
            var desktopReporter = host.Services.GetRequiredService<IInputDesktopReporter>();
            await desktopReporter.Start(agentPipeName, parentProcessId);
            await host.RunAsync();
        }, agentPipeOption, parentIdOption);

        return sidecarCommand;
    }

    private static IHost CreateHost(StartupMode startupMode, string[] args)
    {
        var host = Host.CreateDefaultBuilder(args);
        host.AddControlRAgent(startupMode);
        return host.Build();
    }
}
