using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Startup;
using ControlR.Agent.Shared.Interfaces;
using ControlR.Libraries.Shared.DataValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Startup;

internal static class CommandProvider
{
  internal static Command GetRunCommand(string[] args)
  {
    var instanceIdOption = CreateInstanceIdOption();

    var runCommand = new Command("run", "Run the ControlR service.")
    {
      instanceIdOption
    };

    runCommand.SetAction(async parseResult =>
    {
      var instanceId = parseResult.GetValue(instanceIdOption);
      using var host = CreateHost(StartupMode.Run, args, instanceId);
      await host.RunAsync();
    });

    return runCommand;
  }

  internal static Command GetUninstallCommand(string[] args)
  {
    var instanceIdOption = CreateInstanceIdOption();

    var unInstallCommand = new Command("uninstall", "Uninstall the ControlR service.")
    {
      instanceIdOption
    };

    unInstallCommand.SetAction(async parseResult =>
    {
      var instanceId = parseResult.GetValue(instanceIdOption);
      using var host = CreateHost(StartupMode.Uninstall, args, instanceId);
      var installer = host.Services.GetRequiredService<IAgentInstaller>();

      await installer.Uninstall();

      await WaitForShutdown();
    });

    return unInstallCommand;
  }

  private static IHost CreateHost(
    StartupMode startupMode,
    string[] args,
    string? instanceId = null)
  {
    var host = Host.CreateApplicationBuilder(args);

    host.AddControlRAgent(startupMode, instanceId, serverUri: null);
    return host.Build();
  }

  private static Option<string?> CreateInstanceIdOption()
  {
    var instanceIdOption = new Option<string?>("-i", "--instance-id")
    {
      Description = "The instance ID of the agent, which can be used for multiple agent installations."
    };

    instanceIdOption.Validators.Add(ValidateInstanceId);
    return instanceIdOption;
  }

  private static void ValidateInstanceId(OptionResult optionResult)
  {
    var id = optionResult.GetValueOrDefault<string?>();
    var validationError = Validators.ValidateInstanceId(id);
    if (validationError is not null)
    {
      optionResult.AddError(validationError);
    }
  }

  private static async Task<bool> WaitForKeyPress(TimeSpan timeout)
  {
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < timeout)
    {
      if (Console.KeyAvailable)
      {
        _ = Console.ReadKey(intercept: true);
        return true;
      }
      await Task.Delay(100);
    }
    return false;
  }

  private static async Task WaitForShutdown()
  {
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, eventArgs) =>
    {
      cts.Cancel();
    };

    if (!Environment.UserInteractive)
    {
      Console.WriteLine("Installation completed.  Shutting down.");
      return;
    }

    var timeout = TimeSpan.FromSeconds(5);
    Console.WriteLine($"Application will exit in {timeout.TotalSeconds} seconds. Press any key to interrupt.");

    var keyPressed = await WaitForKeyPress(timeout);
    if (keyPressed)
    {
      Console.WriteLine("Shutdown cancelled by user. Application will continue running.  Press Ctrl+C to exit.");
      await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
    }
    else
    {
      Console.WriteLine("No key pressed; shutting down.");
      cts.Cancel();
    }
  }
}
