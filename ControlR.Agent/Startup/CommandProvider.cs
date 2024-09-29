using System.CommandLine;
using System.CommandLine.Parsing;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Services.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Startup;

internal class CommandProvider
{
  private static readonly string[] _instanceIdAlias = ["-i", "--instance-id"];
  private static readonly string[] _pipeNameAlias = ["-p", "--pipe-name"];
  private static readonly string[] _serverUriAlias = ["-s", "--server-uri"];
  private static readonly string[] _deviceGroupAlias = ["-g", "--device-group"];

  internal static Command GetEchoDesktopCommand(string[] args)
  {
    var pipeNameOption = new Option<string>(
      _pipeNameAlias,
      "The name of the named pipe server to which to send the current input desktop.")
    {
      IsRequired = true
    };

    var echoDesktopCommand =
      new Command("echo-desktop", "Writes the current input desktop to standard out, then exits.")
      {
        pipeNameOption
      };

    echoDesktopCommand.SetHandler(async pipeName =>
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

    var instanceIdOption = new Option<string>(
      _instanceIdAlias,
      "An instance ID for this agent installation, which allows multiple agent installations.  " +
      "This is typically the server origin (e.g. 'example.controlr.app').");

    var deviceGroupOption = new Option<Guid?>(
      _deviceGroupAlias,
      "An optional device group GUID to which the agent should be added.");

    instanceIdOption.AddValidator(ValidateInstanceId);

    var installCommand = new Command("install", "Install the ControlR service.")
    {
      serverUriOption,
      instanceIdOption,
      deviceGroupOption,
    };

    installCommand.SetHandler(async (serverUri, instanceId, deviceGroupId) =>
    {
      using var host = CreateHost(StartupMode.Install, args, instanceId);
      var installer = host.Services.GetRequiredService<IAgentInstaller>();
      await installer.Install(serverUri, deviceGroupId);
      await host.RunAsync();
    }, serverUriOption, instanceIdOption, deviceGroupOption);

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

    runCommand.SetHandler(async instanceId =>
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
    unInstallCommand.SetHandler(async instanceId =>
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
    var host = Host.CreateApplicationBuilder(args);
    host.AddControlRAgent(startupMode, instanceId);
    return host.Build();
  }

  private static void ValidateInstanceId(OptionResult optionResult)
  {
    var id = optionResult.GetValueOrDefault<string>();
    char[] illegalChars = [.. Path.GetInvalidPathChars(), ' '];

    if (id is not null && id.IndexOfAny(illegalChars) >= 0)
    {
      optionResult.ErrorMessage =
        $"The instance ID contains one or more invalid characters: {string.Join(", ", illegalChars)}";
    }
  }
}