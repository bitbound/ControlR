using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace ControlR.Agent.Startup;

internal static class CommandProvider
{
  internal static Command GetInstallCommand(string[] args)
  {
    var serverUriOption = new Option<Uri?>("-s", "--server-uri")
    {
      Description =
        "The fully-qualified server URI to which the agent will connect " +
        "(e.g. 'https://my.example.com' or 'https://my.example.com:8080').",
      CustomParser = result =>
      {
        if (result.Tokens.Count == 0)
        {
          return null;
        }

        var uriArg = result.Tokens[0].Value;
        if (Uri.TryCreate(uriArg, UriKind.Absolute, out var uri))
        {
          return uri;
        }
        result.AddError(
          $"The server URI '{uriArg}' is not a valid absolute URI. " +
          "Please provide a valid URI including the scheme (e.g. 'https://').");

        return null;
      }
    };

    var instanceIdOption = new Option<string>("-i", "--instance-id")
    {
      Description =
        "An instance ID for this agent installation, which allows multiple agent installations.  " +
        "This is typically the server origin (e.g. 'example.controlr.app')."
    };

    instanceIdOption.Validators.Add(ValidateInstanceId);

    var deviceTagsOption = new Option<string?>("-g", "--device-tags")
    {
      Description = "An optional, comma-separated list of tags to which the agent will be assigned."
    };

    var tenantIdOption = new Option<Guid?>("-t", "--tenant-id")
    {
      Required = true,
      Description = "The tenant ID to which the agent will be assigned."
    };

    var installerKeySecretOption = new Option<string?>("-ks", "--installer-key-secret")
    {
      Description = "An access key that will allow the device to be created on the server."
    };

    var installerKeyIdOption = new Option<Guid?>("-ki", "--installer-key-id")
    {
      Description = "The ID of the installer key to use for installation."
    };

    var deviceIdOption = new Option<Guid?>("-d", "--device-id")
    {
      Required = false,
      Description =
        "An optional device ID to which the agent will be assigned.  If omitted, the installer will either " +
        "use the existing device ID saved on the system (if present) or create a new, random ID."
    };

    var installCommand = new Command("install", "Install the ControlR service.")
    {
      serverUriOption,
      instanceIdOption,
      deviceTagsOption,
      tenantIdOption,
      installerKeySecretOption,
      installerKeyIdOption,
      deviceIdOption,
    };

    installCommand.SetAction(async parseResult =>
    {
      var serverUri = parseResult.GetValue(serverUriOption);
      var instanceId = parseResult.GetValue(instanceIdOption);
      var deviceTags = parseResult.GetValue(deviceTagsOption);
      var tenantId = parseResult.GetRequiredValue(tenantIdOption);
      var installerKeySecret = parseResult.GetValue(installerKeySecretOption);
      var installerKeyId = parseResult.GetValue(installerKeyIdOption);
      var deviceId = parseResult.GetValue(deviceIdOption);

      var tags = deviceTags is null
      ? []
      : deviceTags
        .Split(",")
        .Select(x => Guid.TryParse(x, out var tagId)
          ? tagId
          : Guid.Empty)
        .Where(x => x != Guid.Empty)
        .ToArray();

      using var host = CreateHost(StartupMode.Install, args, instanceId, serverUri);
      var installer = host.Services.GetRequiredService<IAgentInstaller>();

      await installer.Install(serverUri, tenantId, installerKeySecret, installerKeyId, deviceId, tags);

      await WaitForShutdown();
    });

    return installCommand;
  }

  internal static Command GetRunCommand(string[] args)
  {
    var instanceIdOption = new Option<string?>("-i", "--instance-id")
    {
      Description = "The instance ID of the agent, which can be used for multiple agent installations."
    };

    instanceIdOption.Validators.Add(ValidateInstanceId);

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
    var instanceIdOption = new Option<string?>("-i", "--instance-id")
    {
      Description = "The instance ID of the agent, which can be used for multiple agent installations."
    };

    instanceIdOption.Validators.Add(ValidateInstanceId);

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
    string? instanceId = null,
    Uri? serverUri = null)
  {
    var host = Host.CreateApplicationBuilder(args);

    host.AddControlRAgent(startupMode, instanceId, serverUri);
    return host.Build();
  }

  private static void ValidateInstanceId(OptionResult optionResult)
  {
    var id = optionResult.GetValueOrDefault<string?>();
    char[] illegalChars = [.. Path.GetInvalidPathChars(), ' '];

    if (id is not null && id.IndexOfAny(illegalChars) >= 0)
    {
      optionResult.AddError(
        $"The instance ID contains one or more invalid characters: {string.Join(", ", illegalChars)}");
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
