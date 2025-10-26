using System.Diagnostics;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Linux;

internal class AgentInstallerLinux(
  IHostApplicationLifetime lifetime,
  IFileSystem fileSystem,
  IProcessManager processManager,
  ISystemEnvironment environmentHelper,
  IControlrApi controlrApi,
  IDeviceDataGenerator deviceDataGenerator,
  IRetryer retryer,
  ISettingsProvider settingsProvider,
  IElevationChecker elevationChecker,
  IEmbeddedResourceAccessor embeddedResourceAccessor,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentInstallerLinux> logger)
  : AgentInstallerBase(fileSystem, controlrApi, deviceDataGenerator, settingsProvider, processManager, appOptions, logger), IAgentInstaller
{
  private static readonly SemaphoreSlim _installLock = new(1, 1);

  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IEmbeddedResourceAccessor _embeddedResourceAccessor = embeddedResourceAccessor;
  private readonly ISystemEnvironment _environment = environmentHelper;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly ILogger<AgentInstallerLinux> _logger = logger;

  public async Task Install(
    Uri? serverUri = null,
    Guid? tenantId = null,
    string? installerKey = null,
    Guid? deviceId = null,
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

      if (!_elevationChecker.IsElevated())
      {
        _logger.LogError("Install command must be run with sudo.");
        return;
      }

      TryClearDotnetExtractDir();

      var installDir = GetInstallDirectory();

      var exePath = _environment.StartupExePath;
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

      // Create DesktopClient directory and copy executable if it exists
      var desktopClientDir = Path.Combine(installDir, "DesktopClient");
      _fileSystem.CreateDirectory(desktopClientDir);

      var desktopClientExeName = AppConstants.DesktopClientFileName;
      var sourceDesktopClientPath = Path.Combine(Path.GetDirectoryName(exePath)!, desktopClientExeName);
      var targetDesktopClientPath = Path.Combine(desktopClientDir, desktopClientExeName);

      if (_fileSystem.FileExists(sourceDesktopClientPath))
      {
        _logger.LogInformation("Copying desktop client executable to {TargetPath}.", targetDesktopClientPath);
        if (_fileSystem.FileExists(targetDesktopClientPath))
        {
          _fileSystem.MoveFile(targetDesktopClientPath, $"{targetDesktopClientPath}.old", true);
        }
        await retryer.Retry(
          () =>
          {
            _fileSystem.CopyFile(sourceDesktopClientPath, targetDesktopClientPath, true);
            return Task.CompletedTask;
          },
          tryCount: 5,
          retryDelay: TimeSpan.FromSeconds(1));
      }
      else
      {
        _logger.LogWarning("Desktop client executable not found at {SourcePath}. User service may not work correctly.", sourceDesktopClientPath);
      }
      var serviceFile = (await GetAgentServiceFile()).Trim();
      var desktopServiceFile = (await GetDesktopServiceFile()).Trim();

      // Ensure service directories exist
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetServiceFilePath())!);
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetDesktopServiceFilePath())!);

      await _fileSystem.WriteAllTextAsync(GetServiceFilePath(), serviceFile);
      await _fileSystem.WriteAllTextAsync(GetDesktopServiceFilePath(), desktopServiceFile);
      await UpdateAppSettings(serverUri, tenantId, deviceId);

      var createResult = await CreateDeviceOnServer(installerKey, tags);
      if (!createResult.IsSuccess)
      {
        return;
      }

      var serviceName = GetServiceName();
      var desktopServiceName = GetDesktopServiceName();

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
        Arguments = $"systemctl enable {serviceName}",
        WorkingDirectory = "/tmp",
        UseShellExecute = true
      };

      _logger.LogInformation("Enabling agent service.");
      await ProcessManager.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

      _logger.LogInformation("Enabling desktop user service.");
      psi.Arguments = $"systemctl --global enable {desktopServiceName}";
      await ProcessManager.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

      _logger.LogInformation("Restarting agent service.");
      psi.Arguments = $"systemctl restart {serviceName}";
      await ProcessManager.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

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
      var desktopServiceName = GetDesktopServiceName();

      await ProcessManager
        .Start("sudo", $"systemctl stop {serviceName}")
        .WaitForExitAsync(_lifetime.ApplicationStopping);

      await ProcessManager
        .Start("sudo", $"systemctl disable {serviceName}")
        .WaitForExitAsync(_lifetime.ApplicationStopping);

      await ProcessManager
        .Start("sudo", $"systemctl --global disable {desktopServiceName}")
        .WaitForExitAsync(_lifetime.ApplicationStopping);

      _fileSystem.DeleteFile(GetServiceFilePath());

      var desktopServicePath = GetDesktopServiceFilePath();
      if (_fileSystem.FileExists(desktopServicePath))
      {
        _fileSystem.DeleteFile(desktopServicePath);
      }

      await ProcessManager
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

  private async Task<string> GetAgentServiceFile()
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerLinux).Assembly,
      "ControlR.Agent.Common.Resources.controlr.agent.service");

    var installDir = GetInstallDirectory();

    var instanceArgs = string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId)
      ? ""
      : $" -i {instanceOptions.Value.InstanceId}";

    template = template
      .Replace("{{INSTALL_DIRECTORY}}", installDir)
      .Replace("{{INSTANCE_ARGS}}", instanceArgs);

    return template;
  }

  private async Task<string> GetDesktopServiceFile()
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerLinux).Assembly,
      "ControlR.Agent.Common.Resources.controlr.desktop.service");

    var installDir = GetInstallDirectory();

    var instanceArgs = string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId)
      ? ""
      : $" --instance-id {instanceOptions.Value.InstanceId}";

    template = template
      .Replace("{{INSTALL_DIRECTORY}}", installDir)
      .Replace("{{INSTANCE_ARGS}}", instanceArgs);

    return template;
  }

  private string GetDesktopServiceFilePath()
  {
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return "/etc/systemd/user/controlr.desktop.service";
    }

    return $"/etc/systemd/user/controlr.desktop-{instanceOptions.Value.InstanceId}.service";
  }

  private string GetDesktopServiceName()
  {
    return Path.GetFileName(GetDesktopServiceFilePath());
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