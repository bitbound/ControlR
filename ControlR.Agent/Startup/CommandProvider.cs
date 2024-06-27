using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace ControlR.Agent.Startup;

internal class CommandProvider
{
    private static readonly string[] _authorizedKeyAlias = ["-a", "--authorized-key"];
    private static readonly string[] _instanceIdAlias = ["-i", "--instance-id"];
    private static readonly string[] _pipeNameAlias = ["-p", "--pipe-name"];
    private static readonly string[] _serverUriAlias = ["-s", "--server-uri"];
    private static readonly string[] _labelAlias = ["-l", "--label"];

    internal static Command GetEchoDesktopCommand(string[] args)
    {
        var pipeNameOption = new Option<string>(
            _pipeNameAlias,
            "The name of the named pipe server to which to send the current input desktop.")
        {
            IsRequired = true,
        };

        var echoDesktopCommand = new Command("echo-desktop", "Writes the current input desktop to standard out, then exits.")
        {
            pipeNameOption
        };

        echoDesktopCommand.SetHandler(async (pipeName) =>
        {
            using var host = CreateHost(StartupMode.EchoDesktop, args);
            await host.StartAsync();
            var desktopEcho = host.Services.GetRequiredService<IDesktopEchoer>();
            await desktopEcho.EchoInputDesktop(pipeName);
        }, pipeNameOption);
        return echoDesktopCommand;
    }

    internal static Command GetInstallCommand(string[] args)
    {
        var serverUriOption = new Option<Uri?>(
             _serverUriAlias,
             "The fully-qualified server URI to which the agent will connect " +
             "(e.g. 'https://my.example.com' or 'http://my.example.com:8080').");

        var authorizedKeyOption = new Option<string?>(
            _authorizedKeyAlias,
            "An optional public key to preconfigure with authorization to this device.");

        var labelOption = new Option<string?>(
            _labelAlias,
            "An optional label to add to the public key (e.g. username), which can make it easier " +
            "to identify key owners when managing access.");

        var instanceIdOption = new Option<string?>(
            _instanceIdAlias,
            "An optional instance ID of the agent, which can be used for multiple agent installations.");
        instanceIdOption.AddValidator(ValidateInstanceId);

        var installCommand = new Command("install", "Install the ControlR service.")
        {
            authorizedKeyOption,
            serverUriOption,
            instanceIdOption,
            labelOption
        };

        installCommand.SetHandler(async (serverUri, authorizedKey, label, instanceId) =>
        {
            using var host = CreateHost(StartupMode.Install, args, instanceId);
            var installer = host.Services.GetRequiredService<IAgentInstaller>();
            await installer.Install(serverUri, authorizedKey, label);
            await host.RunAsync();
        }, serverUriOption, authorizedKeyOption, labelOption, instanceIdOption);

        return installCommand;
    }

    internal static Command GetRunCommand(string[] args)
    {

        var instanceIdOption = new Option<string?>(
            _instanceIdAlias,
            "The instance ID of the agent, which can be used for multiple agent installations.");
        instanceIdOption.AddValidator(ValidateInstanceId);

        var runCommand = new Command("run", "Run the ControlR service.")
        {
            instanceIdOption
        };

        runCommand.SetHandler(async (instanceId) =>
        {
            using var host = CreateHost(StartupMode.Run, args, instanceId);
            await host.RunAsync();
        }, instanceIdOption);

        return runCommand;
    }

    internal static Command GetUninstallCommand(string[] args)
    {
        var instanceIdOption = new Option<string?>(
            _instanceIdAlias,
            "The instance ID of the agent, which can be used for multiple agent installations.");
        instanceIdOption.AddValidator(ValidateInstanceId);

        var unInstallCommand = new Command("uninstall", "Uninstall the ControlR service.")
        {
            instanceIdOption
        };
        unInstallCommand.SetHandler(async (instanceId) =>
        {
            using var host = CreateHost(StartupMode.Uninstall, args, instanceId);
            var installer = host.Services.GetRequiredService<IAgentInstaller>();
            await installer.Uninstall();
            await host.RunAsync();
        }, instanceIdOption);
        return unInstallCommand;
    }

    private static IHost CreateHost(StartupMode startupMode, string[] args, string? instanceId = null)
    {
        var host = Host.CreateDefaultBuilder(args);
        host.AddControlRAgent(startupMode, instanceId);
        return host.Build();
    }

    private static void ValidateInstanceId(OptionResult optionResult)
    {
        var id = optionResult.GetValueOrDefault<string>();
        char[] illegalChars = [.. Path.GetInvalidPathChars(), ' '];

        if (id is not null && id.IndexOfAny(illegalChars) >= 0)
        {
            optionResult.ErrorMessage = $"The instance ID contains one or more invalid characters: {string.Join(", ", illegalChars)}";
        }
    }
}