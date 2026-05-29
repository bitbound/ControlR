using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Shared.Services.Linux;

[SupportedOSPlatform("linux")]
internal class AgentInstallerLinux(
  IHostApplicationLifetime lifetime,
  IFileSystem fileSystem,
  IFileSystemPathProvider fileSystemPathProvider,
  IProcessManager processManager,
  ISystemEnvironment systemEnvironment,
  IControlrApi controlrApi,
  IDeviceInfoProvider deviceDataGenerator,
  IRetryer retryer,
  IOptionsAccessor optionsAcccessor,
  IElevationChecker elevationChecker,
  IServiceControl serviceControl,
  IEmbeddedResourceAccessor embeddedResourceAccessor,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentInstallerLinux> logger)
  : AgentInstallerBase(fileSystem, fileSystemPathProvider, controlrApi, deviceDataGenerator, optionsAcccessor, processManager, systemEnvironment, appOptions, logger), IAgentInstaller
{
  private const string DesktopClientDirectoryName = "DesktopClient";

  private static readonly SemaphoreSlim _installLock = new(1, 1);

  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IEmbeddedResourceAccessor _embeddedResourceAccessor = embeddedResourceAccessor;
  private readonly ISystemEnvironment _environment = systemEnvironment;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly ILogger<AgentInstallerLinux> _logger = logger;
  private readonly IServiceControl _serviceControl = serviceControl;

  public async Task Install(AgentInstallRequest request)
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting.");
      return;
    }

    try
    {
      _logger.LogInformation("Install started.");

      if (!_elevationChecker.IsElevated())
      {
        _logger.LogError("Install command must be run with sudo.");
        return;
      }

      var installDir = GetInstallDirectory();

      var serviceName = GetServiceName();
      var desktopServiceName = GetDesktopServiceName();

      await _serviceControl.StopDesktopClientService(throwOnFailure: false);

      if (_fileSystem.DirectoryExists(installDir))
      {
        await ProcessManager
          .Start("sudo", $"systemctl stop {serviceName}")
          .WaitForExitAsync(_lifetime.ApplicationStopping);

        _fileSystem.DeleteDirectory(installDir, true);
      }

      _fileSystem.CreateDirectory(installDir);

      _logger.LogInformation("Extracting bundle {BundleZipPath} to {InstallDirectory}.", request.BundleZipPath, installDir);
      await retryer.Retry(
        () => ExtractBundleToInstallDirectory(request.BundleZipPath, installDir),
        5,
        TimeSpan.FromSeconds(1));

      SetExecutablePermissions(installDir);

      var serviceFile = (await GetAgentServiceFile()).Trim();
      var desktopServiceFile = (await GetDesktopServiceFile()).Trim();

      // Ensure service directories exist
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetServiceFilePath())!);
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetDesktopServiceFilePath())!);

      await WriteFileIfChanged(GetServiceFilePath(), serviceFile);
      await WriteFileIfChanged(GetDesktopServiceFilePath(), desktopServiceFile);
      await UpdateAppSettings(request.ServerUri, request.TenantId, request.DeviceId);

      var createResult = await CreateDeviceOnServer(request.InstallerKeyId, request.InstallerKeySecret, request.TagIds);
      if (!createResult.IsSuccess)
      {
        return;
      }

      await WriteBundleHashFile(request.BundleSha256);

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

      _logger.LogInformation("Starting desktop user services for logged-in users.");
      await _serviceControl.StartDesktopClientService(throwOnFailure: false);

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

  public async Task RepairDesktopClient(AgentInstallRequest request)
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting desktop repair.");
      return;
    }

    try
    {
      _logger.LogInformation("Desktop repair started.");

      if (!_elevationChecker.IsElevated())
      {
        _logger.LogError("Desktop repair command must be run with sudo.");
        return;
      }

      var installDir = GetInstallDirectory();
      var desktopServiceName = GetDesktopServiceName();
      var stageDirectory = await PrepareRepairStage(request.BundleZipPath, installDir);

      await _serviceControl.StopDesktopClientService(throwOnFailure: false);
      ReplaceDesktopClientDirectory(stageDirectory, installDir);

      var desktopServiceFile = (await GetDesktopServiceFile()).Trim();
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetDesktopServiceFilePath())!);
      await WriteFileIfChanged(GetDesktopServiceFilePath(), desktopServiceFile);
      await WriteBundleHashFile(request.BundleSha256);

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
        WorkingDirectory = "/tmp",
        UseShellExecute = true
      };

      _logger.LogInformation("Reloading systemd daemon for desktop repair.");
      psi.Arguments = "systemctl daemon-reload";
      await ProcessManager.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

      _logger.LogInformation("Enabling desktop user service.");
      psi.Arguments = $"systemctl --global enable {desktopServiceName}";
      await ProcessManager.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

      _logger.LogInformation("Starting desktop user services for logged-in users.");
      await _serviceControl.StartDesktopClientService(throwOnFailure: false);

      _logger.LogInformation("Desktop repair completed.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while repairing the desktop client.");
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
      _logger.LogInformation("Uninstall started.");

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Uninstall command must be run with sudo.");
      }

      var serviceName = GetServiceName();
      var desktopServiceName = GetDesktopServiceName();

      await _serviceControl.StopDesktopClientService(throwOnFailure: false);

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

  internal async Task<string> GetDesktopServiceFile()
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerLinux).Assembly,
      "controlr.desktop.service");

    var installDir = GetInstallDirectory();
    var bundleExtractDir = Path.Combine(installDir, ".net");

    var instanceArgs = string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId)
      ? ""
      : $" --instance-id {instanceOptions.Value.InstanceId}";

    template = template
      .Replace("{{INSTALL_DIRECTORY}}", installDir)
      .Replace("{{BUNDLE_EXTRACT_DIR}}", bundleExtractDir)
      .Replace("{{INSTANCE_ARGS}}", instanceArgs);

    return template;
  }

  private async Task<string> GetAgentServiceFile()
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerLinux).Assembly,
      "controlr.agent.service");

    var installDir = GetInstallDirectory();
    var bundleExtractDir = Path.Combine(installDir, ".net");

    var instanceArgs = string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId)
      ? ""
      : $" -i {instanceOptions.Value.InstanceId}";

    template = template
      .Replace("{{INSTALL_DIRECTORY}}", installDir)
      .Replace("{{BUNDLE_EXTRACT_DIR}}", bundleExtractDir)
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
    return GetInstanceInstallDirectory("/usr/local/bin/ControlR", instanceOptions.Value.InstanceId);
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

  private async Task<string> PrepareRepairStage(string bundleZipPath, string installDir)
  {
    var parentDirectory = Path.GetDirectoryName(installDir)
      ?? throw new DirectoryNotFoundException("Unable to determine the install directory parent.");
    var stageDirectory = Path.Combine(parentDirectory, $".controlr-desktop-repair-{Guid.NewGuid():N}");

    try
    {
      await retryer.Retry(
        () =>
        {
          if (_fileSystem.DirectoryExists(stageDirectory))
          {
            _fileSystem.DeleteDirectory(stageDirectory, true);
          }

          _fileSystem.CreateDirectory(stageDirectory);
          _logger.LogInformation("Extracting repair bundle {BundleZipPath} to {InstallDirectory}.", bundleZipPath, stageDirectory);
          return ExtractBundleToInstallDirectory(bundleZipPath, stageDirectory);
        },
        5,
        TimeSpan.FromSeconds(1));

      var stagedDesktopClientPath = Path.Combine(stageDirectory, DesktopClientDirectoryName, AppConstants.DesktopClientFileName);
      if (!_fileSystem.FileExists(stagedDesktopClientPath))
      {
        throw new FileNotFoundException("The repair bundle does not contain the desktop client executable.", stagedDesktopClientPath);
      }

      return stageDirectory;
    }
    catch
    {
      TryDeleteDirectory(stageDirectory);
      throw;
    }
  }

  private void ReplaceDesktopClientDirectory(string stageDirectory, string installDir)
  {
    var destinationDirectory = Path.Combine(installDir, DesktopClientDirectoryName);
    var stagedDirectory = Path.Combine(stageDirectory, DesktopClientDirectoryName);
    var backupDirectory = $"{destinationDirectory}.backup-{Guid.NewGuid():N}";

    try
    {
      _fileSystem.CreateDirectory(installDir);

      if (_fileSystem.DirectoryExists(destinationDirectory))
      {
        _fileSystem.MoveDirectory(destinationDirectory, backupDirectory);
      }

      _fileSystem.MoveDirectory(stagedDirectory, destinationDirectory);

      TryDeleteDirectory(backupDirectory);
    }
    catch
    {
      if (!_fileSystem.DirectoryExists(destinationDirectory) && _fileSystem.DirectoryExists(backupDirectory))
      {
        Directory.Move(backupDirectory, destinationDirectory);
      }

      throw;
    }
    finally
    {
      TryDeleteDirectory(stageDirectory);
    }
  }

  private void SetExecutablePermissions(string installDirectory)
  {
    var executableFileMode =
      UnixFileMode.UserRead |
      UnixFileMode.UserWrite |
      UnixFileMode.UserExecute |
      UnixFileMode.GroupRead |
      UnixFileMode.GroupExecute |
      UnixFileMode.OtherRead |
      UnixFileMode.OtherExecute;

    foreach (var executablePath in new[]
    {
      Path.Combine(installDirectory, "ControlR.Agent"),
      Path.Combine(installDirectory, "DesktopClient", "ControlR.DesktopClient")
    })
    {
      if (!_fileSystem.FileExists(executablePath))
      {
        continue;
      }

      _fileSystem.SetUnixFileMode(executablePath, executableFileMode);
      _logger.LogDebug("Set executable permissions on {FilePath}", executablePath);
    }
  }

  private void TryDeleteDirectory(string directoryPath)
  {
    try
    {
      if (_fileSystem.DirectoryExists(directoryPath))
      {
        _fileSystem.DeleteDirectory(directoryPath, true);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to delete temporary directory {DirectoryPath}.", directoryPath);
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
