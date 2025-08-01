﻿using System.Diagnostics;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Options;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesNative.Linux;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Linux;

internal class AgentInstallerLinux(
  IHostApplicationLifetime lifetime,
  IFileSystem fileSystem,
  IProcessManager processInvoker,
  ISystemEnvironment environmentHelper,
  IControlrApi controlrApi,
  IDeviceDataGenerator deviceDataGenerator,
  IRetryer retryer,
  ISettingsProvider settingsProvider,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentInstallerLinux> logger)
  : AgentInstallerBase(fileSystem, controlrApi, deviceDataGenerator, settingsProvider, appOptions, logger), IAgentInstaller
{
  private static readonly SemaphoreSlim _installLock = new(1, 1);
  private readonly ISystemEnvironment _environment = environmentHelper;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly ILogger<AgentInstallerLinux> _logger = logger;
  private readonly IProcessManager _processInvoker = processInvoker;

  public async Task Install(
    Uri? serverUri = null,
    Guid? tenantId = null,
    string? installerKey = null,
    Guid[]? tags = null)
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting.");
      return;
    }

    try
    {
      if (serverUri is null && AppOptions.CurrentValue.ServerUri is null)
      {
        Logger.LogWarning(
          "The ServerUri needs to be provided either via command line arguments or installed appsettings file.  " +
          "Aborting installation.");
        return;
      }

      _logger.LogInformation("Install started.");

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Install command must be run with sudo.");
        return;
      }

      TryClearDotnetExtractDir();

      var installDir = GetInstallDirectory();

      var exePath = _environment.StartupExePath;
      var fileName = Path.GetFileName(exePath);
      var targetPath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environment.Platform));
      _fileSystem.CreateDirectory(installDir);

      if (_fileSystem.FileExists(targetPath))
      {
        _fileSystem.MoveFile(targetPath, $"{targetPath}.old", true);
      }

      _logger.LogInformation("Copying agent executable to {TargetPath}.", targetPath);
      await retryer.Retry(
        () =>
        {
          _fileSystem.CopyFile(exePath, targetPath, true);
          return Task.CompletedTask;
        }, 5, TimeSpan.FromSeconds(1));

      var serviceFile = GetServiceFile().Trim();

      await _fileSystem.WriteAllTextAsync(GetServiceFilePath(), serviceFile);
      await UpdateAppSettings(serverUri, tenantId);

      var createResult = await CreateDeviceOnServer(installerKey, tags);
      if (!createResult.IsSuccess)
      {
        return;
      }

      var serviceName = GetServiceName();

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
        Arguments = $"systemctl enable {serviceName}",
        WorkingDirectory = "/tmp",
        UseShellExecute = true
      };

      _logger.LogInformation("Enabling service.");
      await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

      _logger.LogInformation("Restarting service.");
      psi.Arguments = $"systemctl restart {serviceName}";
      await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

      _logger.LogInformation("Install completed.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while installing the ControlR service.");
    }
    finally
    {
      _lifetime.StopApplication();
      _installLock.Release();
    }
  }

  public async Task Uninstall()
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting.");
      return;
    }

    try
    {
      _logger.LogInformation("Uninstall started.");

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Uninstall command must be run with sudo.");
      }

      var serviceName = GetServiceName();

      await _processInvoker
        .Start("sudo", $"systemctl stop {serviceName}")
        .WaitForExitAsync(_lifetime.ApplicationStopping);

      await _processInvoker
        .Start("sudo", $"systemctl disable {serviceName}")
        .WaitForExitAsync(_lifetime.ApplicationStopping);

      _fileSystem.DeleteFile(GetServiceFilePath());

      await _processInvoker
        .Start("sudo", "systemctl daemon-reload")
        .WaitForExitAsync(_lifetime.ApplicationStopping);

      _fileSystem.DeleteDirectory(GetInstallDirectory(), true);

      _logger.LogInformation("Uninstall completed.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while uninstalling the ControlR service.");
    }
    finally
    {
      _lifetime.StopApplication();
      _installLock.Release();
    }
  }

  private string GetInstallDirectory()
  {
    var dir = "/usr/local/bin/ControlR";
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return dir;
    }

    return Path.Combine(dir, instanceOptions.Value.InstanceId);
  }

  private string GetServiceFile()
  {
    var installDir = GetInstallDirectory();
    var fileName = AppConstants.GetAgentFileName(_environment.Platform);

    var runCommand = "run";
    if (instanceOptions.Value.InstanceId is string instanceId)
    {
      runCommand += $" -i {instanceId}";
    }

    return
      "[Unit]\n" +
      "Description=ControlR provides zero-trust remote control and administration.\n\n" +
      "[Service]\n" +
      $"WorkingDirectory={installDir}\n" +
      $"ExecStart={installDir}/{fileName} {runCommand}\n" +
      "Restart=always\n" +
      "StartLimitIntervalSec=0\n" +
      "Environment=DOTNET_ENVIRONMENT=Production\n" +
      "RestartSec=10\n\n" +
      "[Install]\n" +
      "WantedBy=graphical.target";
  }

  private string GetServiceFilePath()
  {
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return "/etc/systemd/system/controlr.agent.service";
    }

    return $"/etc/systemd/system/controlr.agent-{instanceOptions.Value.InstanceId}.service";
  }

  private string GetServiceName()
  {
    return Path.GetFileName(GetServiceFilePath());
  }

  private void TryClearDotnetExtractDir()
  {
    try
    {
      if (_fileSystem.DirectoryExists("/root/.net/ControlR.Agent"))
      {
        var subdirs = _fileSystem
          .GetDirectories("/root/.net/ControlR.Agent")
          .Select(x => new DirectoryInfo(x))
          .OrderByDescending(x => x.CreationTime)
          .Skip(3)
          .ToArray();

        foreach (var subdir in subdirs)
        {
          try
          {
            subdir.Delete(true);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to delete directory {SubDir}.", subdir);
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while cleaning up .net extraction directory.");
    }
  }
}