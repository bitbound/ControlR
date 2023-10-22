using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services;
using ControlR.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.Reflection;

namespace ControlR.Agent.Startup;

internal class CommandProvider
{
    private static readonly string[] _authorizedKeyAlias = ["-a", "--authorized-key"];
    private static readonly string[] _autoInstallVncAlis = ["-i", "--auto-install"];
    private static readonly string[] _portAlias = ["-p", "--port"];

    internal static Command GetInstallCommand(string[] args)
    {
        var authorizedKeyOption = new Option<string>(
            _authorizedKeyAlias,
            "An optional public key to preconfigure with authorization to this device.");

        var portOption = new Option<int>(
            _portAlias,
            () => 5900,
            "The port to use for the spawned VNC.  ControlR will proxy viewer connections to this port.");

        var autoInstallOption = new Option<bool>(
             _autoInstallVncAlis,
             () => true,
             "Whether to automatically install and run a temporary VNC server with a random, temporary password.  " +
             "Each session will have a new random password.  Set this to false to use an existing server without  " +
             "altering it.  Only works on Windows and X11.");

        var installCommand = new Command("install", "Install the ControlR service.")
        {
            authorizedKeyOption,
            portOption,
            autoInstallOption
        };

        installCommand.SetHandler(async (authorizedKey, vncPort, auotInstallVnc) =>
        {
            var host = CreateHost(StartupMode.Install, args);
            var installer = host.Services.GetRequiredService<IAgentInstaller>();
            await installer.Install(authorizedKey, vncPort, auotInstallVnc);
            await host.RunAsync();
        }, authorizedKeyOption, portOption, autoInstallOption);

        return installCommand;
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