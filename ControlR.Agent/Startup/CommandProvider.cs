using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

namespace ControlR.Agent.Startup;

internal class CommandProvider
{
    private static readonly string[] _authorizedKeyAlias = ["-a", "--authorized-key"];
    private static readonly string[] _autoRunVncAlias = ["-r", "--auto-run"];
    private static readonly string[] _serverUriAlias = ["-s", "--server-uri"];
    private static readonly string[] _vncPortAlias = ["-v", "--vnc-port"];

    internal static Command GetInstallCommand(string[] args)
    {
        var serverUriOption = new Option<Uri?>(
             _serverUriAlias,
             "The fully-qualified server URI to which the agent will connect " +
             "(e.g. 'https://my.example.com' or 'http://my.example.com:8080'). ");

        var authorizedKeyOption = new Option<string>(
            _authorizedKeyAlias,
            "An optional public key to preconfigure with authorization to this device.");

        var vncPortOption = new Option<int?>(
            _vncPortAlias,
            "The port to use for VNC connections.  ControlR will proxy viewer connections to this port.");

        var autoRunOption = new Option<bool?>(
             _autoRunVncAlias,
             "Whether to automatically download (if needed) and run a temporary TightVNC server. " +
             "The server will run in loopback-only mode, and a new random password will be generated " +
             "for each session. The server will shutdown when the session ends. Set this to false " +
             "to use an existing server.");

        var installCommand = new Command("install", "Install the ControlR service.")
        {
            authorizedKeyOption,
            vncPortOption,
            autoRunOption,
            serverUriOption
        };

        installCommand.SetHandler(async (serverUri, authorizedKey, vncPort, autoRunVnc) =>
        {
            var host = CreateHost(StartupMode.Install, args);
            var installer = host.Services.GetRequiredService<IAgentInstaller>();
            await installer.Install(serverUri, authorizedKey, vncPort, autoRunVnc);
            await host.RunAsync();
        }, serverUriOption, authorizedKeyOption, vncPortOption, autoRunOption);

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