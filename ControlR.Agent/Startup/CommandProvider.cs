﻿using System.CommandLine;
using System.CommandLine.Parsing;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Startup;

internal static class CommandProvider
{
  private static readonly string[] _deviceTagsAlias = ["-g", "--device-tags"];
  private static readonly string[] _installerKeyAlias = ["-k", "--installer-key"];
  private static readonly string[] _instanceIdAlias = ["-i", "--instance-id"];
  private static readonly string[] _serverUriAlias = ["-s", "--server-uri"];
  private static readonly string[] _tenantIdAlias = ["-t", "--tenant-id"];

  internal static Command GetInstallCommand(string[] args)
  {
    var serverUriOption = new Option<Uri?>(
      "ServerUri",
      _serverUriAlias)
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

    var instanceIdOption = new Option<string>(
      "InstanceId",
      _instanceIdAlias)
    {
      Description = 
        "An instance ID for this agent installation, which allows multiple agent installations.  " +
        "This is typically the server origin (e.g. 'example.controlr.app')."
    };

    instanceIdOption.Validators.Add(ValidateInstanceId);

    var deviceTagsOption = new Option<string?>(
      "DeviceTags",
      _deviceTagsAlias)
    {
      Description = "An optional, comma-separated list of tags to which the agent will be assigned."
    };

    var tenantIdOption = new Option<Guid?>(
      "TenantId",
      _tenantIdAlias)
    {
      Required = true,
      Description = "The tenant ID to which the agent will be assigned."
    };

    var installerKeyOption = new Option<string?>(
      "InstallerKey",
      _installerKeyAlias)
    {
      Description = "An access key that will allow the device to be created on the server."
    };


    var installCommand = new Command("install", "Install the ControlR service.")
    {
      serverUriOption,
      instanceIdOption,
      deviceTagsOption,
      tenantIdOption,
      installerKeyOption,
    };

    installCommand.SetAction(async parseResult =>
    {
      var serverUri = parseResult.GetValue(serverUriOption);
      var instanceId = parseResult.GetValue(instanceIdOption);
      var deviceTags = parseResult.GetValue(deviceTagsOption);
      var tenantId = parseResult.GetRequiredValue(tenantIdOption);
      var installerKey = parseResult.GetValue(installerKeyOption);

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
      await installer.Install(serverUri, tenantId, installerKey, tags);
      await host.RunAsync();
    });

    return installCommand;
  }

  internal static Command GetRunCommand(string[] args)
  {
    var instanceIdOption = new Option<string?>(
      "InstanceId",
      _instanceIdAlias)
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
    var instanceIdOption = new Option<string?>(
      "InstanceId",
      _instanceIdAlias)
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
      await host.RunAsync();
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
    var id = optionResult.GetValueOrDefault<string>();
    char[] illegalChars = [.. Path.GetInvalidPathChars(), ' '];

    if (id is not null && id.IndexOfAny(illegalChars) >= 0)
    {
      optionResult.AddError(
        $"The instance ID contains one or more invalid characters: {string.Join(", ", illegalChars)}");
    }
  }
}