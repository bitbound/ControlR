using System.Diagnostics;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Options;
using ControlR.Agent.Services.Base;
using ControlR.Devices.Native.Linux;
using ControlR.Libraries.Shared.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services.Mac;

internal class AgentInstallerMac(
  IHostApplicationLifetime lifetime,
  IFileSystem fileSystem,
  IProcessManager processInvoker,
  IEnvironmentHelper environmentHelper,
  IRetryer retryer,
  ISettingsProvider settingsProvider,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentInstallerMac> logger)
  : AgentInstallerBase(fileSystem, settingsProvider, appOptions, logger), IAgentInstaller
{
  private static readonly SemaphoreSlim _installLock = new(1, 1);
  private readonly IEnvironmentHelper _environment = environmentHelper;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly ILogger<AgentInstallerMac> _logger = logger;
  private readonly IProcessManager _processInvoker = processInvoker;

  public async Task Install(Uri? serverUri = null, Guid? deviceGroupId = null)
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
      }

      var installDir = GetInstallDirectory();

      var exePath = _environment.StartupExePath;
      var fileName = Path.GetFileName(exePath);
      var targetPath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environment.Platform));
      _fileSystem.CreateDirectory(installDir);

      if (_fileSystem.FileExists(targetPath))
      {
        _fileSystem.MoveFile(targetPath, $"{targetPath}.old", true);
      }

      await retryer.Retry(
        () =>
        {
          _fileSystem.CopyFile(exePath, targetPath, true);
          return Task.CompletedTask;
        }, 5, TimeSpan.FromSeconds(1));

      var serviceFile = GetServiceFile().Trim();
      var serviceFilePath = GetServiceFilePath();

      _logger.LogInformation("Writing service file.");
      await _fileSystem.WriteAllTextAsync(serviceFilePath, serviceFile);
      await UpdateAppSettings(serverUri);
      await CreateDeviceOnServer(serverUri, deviceGroupId);

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
        WorkingDirectory = "/tmp",
        UseShellExecute = true
      };

      try
      {
        _logger.LogInformation("Bootstrapping service.");
        psi.Arguments = $"launchctl bootstrap system {serviceFilePath}";
        await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));
      }
      catch (ProcessStatusException)
      {
      }

      _logger.LogInformation("Kickstarting service.");
      psi.Arguments = "launchctl kickstart -k system/dev.jaredg.controlr-agent";
      _ = _processInvoker.Start(psi);

      _logger.LogInformation("Installer finished.");
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

      var serviceFilePath = GetServiceFilePath();

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
        Arguments = $"launchctl bootout system {serviceFilePath}",
        WorkingDirectory = "/tmp",
        UseShellExecute = true
      };

      try
      {
        _logger.LogInformation("Booting out service.");
        psi.Arguments = $"launchctl bootout system {serviceFilePath}";
        await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));
      }
      catch (ProcessStatusException)
      {
      }

      if (_fileSystem.FileExists(serviceFilePath))
      {
        _fileSystem.DeleteFile(serviceFilePath);
      }

      var installDir = GetInstallDirectory();
      if (_fileSystem.DirectoryExists(installDir))
      {
        _fileSystem.DeleteDirectory(installDir, true);
      }

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
    var runCommand = "run";
    if (instanceOptions.Value.InstanceId is string instanceId)
    {
      runCommand += $" -i {instanceId}";
    }

    return
      $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
      $"<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
      $"<plist version=\"1.0\">\n" +
      $"<dict>\n" +
      $"    <key>Label</key>\n" +
      $"    <string>dev.jaredg.controlr-agent</string>\n" +
      $"    <key>KeepAlive</key>\n" +
      $"    <true/>\n" +
      //$"    <key>StandardErrorPath</key>\n" +
      //$"    <string>/var/log/ControlR/plist-err.log</string>\n" +
      //$"    <key>StandardOutPath</key>\n" +
      //$"    <string>/var/log/ControlR/plist-std-log</string> \n" +
      $"    <key>ProgramArguments</key>\n" +
      $"    <array>\n" +
      $"        <string>{GetInstallDirectory()}/ControlR.Agent</string>\n" +
      $"        <string>{runCommand}</string>\n" +
      $"    </array>\n" +
      $"</dict>\n" +
      $"</plist>";
  }

  private string GetServiceFilePath()
  {
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchDaemons/controlr-agent.plist";
    }

    return $"/Library/LaunchDaemons/controlr-agent-{instanceOptions.Value.InstanceId}.plist";
  }

  private string GetServiceName()
  {
    return Path.GetFileName(GetServiceFilePath());
  }
}