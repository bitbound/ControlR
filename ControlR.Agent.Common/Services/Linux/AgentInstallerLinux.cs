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
  ISystemEnvironment systemEnvironment,
  IControlrApi controlrApi,
  IDeviceDataGenerator deviceDataGenerator,
  IRetryer retryer,
  ISettingsProvider settingsProvider,
  IElevationChecker elevationChecker,
  IEmbeddedResourceAccessor embeddedResourceAccessor,
  IControlrMutationLock mutationLock,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentInstallerLinux> logger)
  : AgentInstallerBase(fileSystem, controlrApi, deviceDataGenerator, settingsProvider, processManager, appOptions, logger), IAgentInstaller
{
  private static readonly SemaphoreSlim _installLock = new(1, 1);

  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IEmbeddedResourceAccessor _embeddedResourceAccessor = embeddedResourceAccessor;
  private readonly ISystemEnvironment _environment = systemEnvironment;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly ILogger<AgentInstallerLinux> _logger = logger;
  private readonly IControlrMutationLock _mutationLock = mutationLock;

  public async Task Install(
    Uri? serverUri = null,
    Guid? tenantId = null,
    string? installerKeySecret = null,
    Guid? installerKeyId = null,
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
      using var mutation = await _mutationLock.AcquireAsync(_lifetime.ApplicationStopping);
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

      var startupExePath = _environment.StartupExePath;
      var targetAgentPath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environment.Platform));
      _fileSystem.CreateDirectory(installDir);

      if (_fileSystem.FileExists(targetAgentPath))
      {
        _fileSystem.MoveFile(targetAgentPath, $"{targetAgentPath}.old", true);
      }

      _logger.LogInformation("Copying agent executable to {TargetPath}.", targetAgentPath);
      await retryer.Retry(
        () =>
        {
          _fileSystem.CopyFile(startupExePath, targetAgentPath, true);
          return Task.CompletedTask;
        }, 5, TimeSpan.FromSeconds(1));
      
      var serviceFile = (await GetAgentServiceFile()).Trim();
      var desktopServiceFile = (await GetDesktopServiceFile()).Trim();

      // Ensure service directories exist
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetServiceFilePath())!);
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetDesktopServiceFilePath())!);

      await WriteFileIfChanged(GetServiceFilePath(), serviceFile);
      await WriteFileIfChanged(GetDesktopServiceFilePath(), desktopServiceFile);
      await UpdateAppSettings(serverUri, tenantId, deviceId);

      var createResult = await CreateDeviceOnServer(installerKeyId, installerKeySecret, tags);
      if (!createResult.IsSuccess)
      {
        return;
      }

      var serviceName = GetServiceName();
      var desktopServiceName = GetDesktopServiceName();

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
        WorkingDirectory = "/tmp",
        UseShellExecute = true
      };

      _logger.LogInformation("Reloading systemd daemon.");
      psi.Arguments = "systemctl daemon-reload";
      await ProcessManager.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

      _logger.LogInformation("Enabling agent service.");
      psi.Arguments = $"systemctl enable {serviceName}";
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
      using var mutation = await _mutationLock.AcquireAsync(_lifetime.ApplicationStopping);
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

  private async Task WriteFileIfChanged(string filePath, string content)
  {
    if (_fileSystem.FileExists(filePath))
    {
      var existingContent = await _fileSystem.ReadAllTextAsync(filePath);
      if (existingContent.Trim() == content.Trim())
      {
        _logger.LogInformation("File {FilePath} already exists with the same content. Skipping write.", filePath);
        return;
      }
    }

    _logger.LogInformation("Writing file {FilePath}.", filePath);
    await _fileSystem.WriteAllTextAsync(filePath, content);
  }
}