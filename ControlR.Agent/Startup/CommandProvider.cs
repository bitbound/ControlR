using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

namespace ControlR.Agent.Startup;

internal class CommandProvider
{
    private static readonly string[] _authorizedKeyAlias = ["-a", "--authorized-key"];
    private static readonly string[] _serverUriAlias = ["-s", "--server-uri"];
    internal static Command GetInstallCommand(string[] args)
    {
        var serverUriOption = new Option<Uri?>(
             _serverUriAlias,
             "The fully-qualified server URI to which the agent will connect " +
             "(e.g. 'https://my.example.com' or 'http://my.example.com:8080'). ");

        var authorizedKeyOption = new Option<string>(
            _authorizedKeyAlias,
            "An optional public key to preconfigure with authorization to this device.");

        var installCommand = new Command("install", "Install the ControlR service.")
        {
            authorizedKeyOption,
            serverUriOption
        };

        installCommand.SetHandler(async (serverUri, authorizedKey) =>
        {
            var host = CreateHost(StartupMode.Install, args);
            var installer = host.Services.GetRequiredService<IAgentInstaller>();
            await installer.Install(serverUri, authorizedKey);
            await host.RunAsync();
        }, serverUriOption, authorizedKeyOption);

        return installCommand;
    }

    internal static Command GetRunCommand(string[] args)
    {
        var runCommand = new Command("run", "Run the ControlR service.");

        runCommand.SetHandler(async () =>
        {
            var host = CreateHost(StartupMode.Run, args);
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

    private static IHost CreateHost(StartupMode startupMode, string[] args)
    {
        var host = Host.CreateDefaultBuilder(args);
        host.AddControlRAgent(startupMode);
        return host.Build();
    }
}