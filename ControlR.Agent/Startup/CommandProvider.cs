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
    private static readonly string[] _pipeNameAlias = ["-p", "--pipe-name"];

    internal static Command GetEchoDesktopCommand(string[] args)
    {
        var pipeNameOption = new Option<string>(
            _pipeNameAlias,
            "The name of the named pipe server to which to send the current input desktop.");

        var echoDesktopCommand = new Command("echo-desktop", "Writes the current input desktop to standard out, then exits.")
        {
            pipeNameOption
        };

        echoDesktopCommand.SetHandler(async (pipeName) =>
        {
            var host = CreateHost(StartupMode.EchoDesktop, args);
            var desktopEcho = host.Services.GetRequiredService<IDesktopEchoer>();
            await host.StartAsync();
            await desktopEcho.EchoInputDesktop(pipeName);

        }, pipeNameOption);
        return echoDesktopCommand;
    }

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