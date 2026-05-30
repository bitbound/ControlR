using ControlR.Agent.Shared.Constants;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace ControlR.Agent.Shared.Services.Mac;

[SupportedOSPlatform("macos")]
internal class AgentInstallerMac(
  IFileSystem fileSystem,
  IFileSystemPathProvider fileSystemPathProvider,
  IServiceControl serviceControl,
  IRetryer retryer,
  IControlrApi controlrApi,
  IEmbeddedResourceAccessor embeddedResourceAccessor,
  IDeviceInfoProvider deviceDataGenerator,
  IOptionsAccessor optionsAccessor,
  IProcessManager processManager,
  ISystemEnvironment systemEnvironment,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentInstallerMac> logger)
  : AgentInstallerBase(fileSystem, fileSystemPathProvider, controlrApi, deviceDataGenerator, optionsAccessor, processManager, systemEnvironment, appOptions, logger), IAgentInstaller
{
  private const string MacInstallerTempDirectory = "/tmp/ControlR_Update";

  private static readonly SemaphoreSlim _installLock = new(1, 1);

  private readonly IEmbeddedResourceAccessor _embeddedResourceAccessor = embeddedResourceAccessor;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<AgentInstallerMac> _logger = logger;
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

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Install command must be run with sudo.");
        return;
      }

      var appBundleInstallPath = GetInstalledAppBundlePath();
      var tempExtractDirectory = GetTempExtractDirectory();
      var installedAgentPath = GetInstalledAgentPath();
      var installedAgentDirectory = Path.GetDirectoryName(installedAgentPath)
        ?? throw new DirectoryNotFoundException("Unable to determine the agent install directory.");
      var bundleStateDirectory = GetBundleStateDirectory();

      await _serviceControl.StopDesktopClientService(throwOnFailure: false);
      await _serviceControl.StopAgentService(throwOnFailure: false);

      if (_fileSystem.DirectoryExists(appBundleInstallPath))
      {
        _fileSystem.DeleteDirectory(appBundleInstallPath, true);
      }

      if (_fileSystem.FileExists(installedAgentPath))
      {
        _fileSystem.DeleteFile(installedAgentPath);
      }

      _fileSystem.CreateDirectory(installedAgentDirectory);
      _fileSystem.CreateDirectory(bundleStateDirectory);
      _fileSystem.CreateDirectory(tempExtractDirectory);

      try
      {
        var tempAppBundlePath = Path.Combine(tempExtractDirectory, "ControlR.app");
        await retryer.Retry(
          async () => {
            await ExtractBundleToInstallDirectory(request.BundleZipPath, tempExtractDirectory);
            _fileSystem.MoveDirectory(tempAppBundlePath, appBundleInstallPath);
          },
          tryCount: 5,
          retryDelay: TimeSpan.FromSeconds(1));

        SetExecutablePermissions(appBundleInstallPath);

      }
      finally
      {
        if (_fileSystem.DirectoryExists(tempExtractDirectory))
        {
          _fileSystem.DeleteDirectory(tempExtractDirectory, true);
        }
      }

      var sourceAgentPath = GetSourceAgentPath(appBundleInstallPath);

      _logger.LogInformation("Installing agent executable to {AgentPath}.", installedAgentPath);
      _fileSystem.CopyFile(sourceAgentPath, installedAgentPath, overwrite: true);
      SetAgentPermissions(installedAgentPath);

      var agentPlistPath = GetLaunchDaemonFilePath();
      var agentPlistFile = (await GetLaunchDaemonFile()).Trim();
      var desktopPlistPath = GetLaunchAgentFilePath();
      var desktopPlistFile = (await GetLaunchAgentFile()).Trim();
      var installerPlistPath = GetInstallerDaemonFilePath();
      var installerPlistFile = (await GetInstallerDaemonFile(request)).Trim();

      _logger.LogInformation("Writing plist files.");
      await WriteFileIfChanged(agentPlistPath, agentPlistFile);
      await WriteFileIfChanged(desktopPlistPath, desktopPlistFile);
      await WriteFileIfChanged(installerPlistPath, installerPlistFile);
      await UpdateAppSettings(request.ServerUri, request.TenantId, request.DeviceId);

      var createResult = await CreateDeviceOnServer(request.InstallerKeyId, request.InstallerKeySecret, request.TagIds);
      if (!createResult.IsSuccess)
      {
        return;
      }

      await WriteBundleHashFile(request.BundleSha256);

      await _serviceControl.StartAgentService(throwOnFailure: false);
      await _serviceControl.StartDesktopClientService(throwOnFailure: false);

      _logger.LogInformation("Installer finished.");
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

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Desktop repair command must be run with sudo.");
        return;
      }

      var installedAppBundlePath = GetInstalledAppBundlePath();
      var stageDirectory = GetRepairStageDirectory();
      var stagedAppBundlePath = _fileSystem.JoinPaths('/', stageDirectory, PathConstants.GetMacAppBundleName(_instanceOptions.Value.InstanceId));
      var backupAppBundlePath = $"{installedAppBundlePath}.backup-{Guid.NewGuid():N}";

      try
      {
        await retryer.Retry(
          () =>
          {
            TryDeleteDirectory(stageDirectory);
            _fileSystem.CreateDirectory(stageDirectory);
            _logger.LogInformation("Extracting repair bundle {BundleZipPath} to {InstallDirectory}.", request.BundleZipPath, stageDirectory);
            return ExtractBundleToInstallDirectory(request.BundleZipPath, stageDirectory);
          },
          tryCount: 5,
          retryDelay: TimeSpan.FromSeconds(1));

        ValidateRepairedAppBundle(stagedAppBundlePath);

        await _serviceControl.StopDesktopClientService(throwOnFailure: false);

        if (_fileSystem.DirectoryExists(installedAppBundlePath))
        {
          _fileSystem.MoveDirectory(installedAppBundlePath, backupAppBundlePath);
        }

        _fileSystem.MoveDirectory(stagedAppBundlePath, installedAppBundlePath);

        TryDeleteDirectory(backupAppBundlePath);
      }
      catch
      {
        if (!_fileSystem.DirectoryExists(installedAppBundlePath) && _fileSystem.DirectoryExists(backupAppBundlePath))
        {
          Directory.Move(backupAppBundlePath, installedAppBundlePath);
        }

        throw;
      }
      finally
      {
        TryDeleteDirectory(stageDirectory);
      }

      var desktopPlistPath = GetLaunchAgentFilePath();
      var desktopPlistFile = (await GetLaunchAgentFile()).Trim();
      await WriteFileIfChanged(desktopPlistPath, desktopPlistFile);
      await WriteBundleHashFile(request.BundleSha256);
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

      var serviceFilePath = GetLaunchDaemonFilePath();
      var desktopFilePath = GetLaunchAgentFilePath();
      var installerFilePath = GetInstallerDaemonFilePath();

      _logger.LogInformation("Booting out services.");
      await _serviceControl.StopDesktopClientService(throwOnFailure: false);
      await _serviceControl.StopAgentService(throwOnFailure: false);
      await StopInstallerDaemonService();

      if (_fileSystem.FileExists(serviceFilePath))
      {
        _fileSystem.DeleteFile(serviceFilePath);
      }

      if (_fileSystem.FileExists(desktopFilePath))
      {
        _fileSystem.DeleteFile(desktopFilePath);
      }

      if (_fileSystem.FileExists(installerFilePath))
      {
        _fileSystem.DeleteFile(installerFilePath);
      }

      var appBundleInstallPath = GetInstalledAppBundlePath();
      if (_fileSystem.DirectoryExists(appBundleInstallPath))
      {
        _fileSystem.DeleteDirectory(appBundleInstallPath, true);
      }

      var installedAgentPath = GetInstalledAgentPath();
      if (_fileSystem.FileExists(installedAgentPath))
      {
        _fileSystem.DeleteFile(installedAgentPath);
      }

      var bundleStateDirectory = GetBundleStateDirectory();
      if (_fileSystem.DirectoryExists(bundleStateDirectory))
      {
        _fileSystem.DeleteDirectory(bundleStateDirectory, true);
      }

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

  internal async Task<string> GetLaunchAgentFile()
  {
    var serviceName = GetDesktopServiceName();

    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerMac).Assembly,
      "LaunchAgent.plist");

    var bundleExtractDir = FilesystemPathProvider.GetDotnetExtractDirectory();

    template = template
      .Replace("{{SERVICE_NAME}}", serviceName)
      .Replace("{{DESKTOP_EXECUTABLE_PATH}}", FilesystemPathProvider.GetDesktopExecutablePath())
      .Replace("{{DOTNET_BUNDLE_EXTRACT_BASE_DIR}}", bundleExtractDir);

    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      // Remove lines containing {{INSTANCE_ID}} and <string>--instance-id</string>
      var lines = template.Split('\n');
      lines = [.. lines.Where(line =>
        !line.Contains("{{INSTANCE_ID}}") &&
        !line.Contains("<string>--instance-id</string>"))];

      template = string.Join("\n", lines);
    }
    else
    {
      template = template.Replace("{{INSTANCE_ID}}", _instanceOptions.Value.InstanceId);
    }
    return template;
  }

  private string GetAgentServiceName()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? "app.controlr.agent"
      : $"app.controlr.agent.{_instanceOptions.Value.InstanceId}";
  }

  private string GetBundleStateDirectory()
  {
    return FilesystemPathProvider.GetBundleStateDirectory();
  }

  private string GetDesktopServiceName()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? "app.controlr.desktop"
      : $"app.controlr.desktop.{_instanceOptions.Value.InstanceId}";
  }

  private string GetInstalledAgentPath()
  {
    return FilesystemPathProvider.GetAgentExecutablePath();
  }

  private string GetInstalledAppBundlePath()
  {
    return FilesystemPathProvider.GetMacAppBundlePath();
  }

  private async Task<string> GetInstallerDaemonFile(AgentInstallRequest request)
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerMac).Assembly,
      "InstallerDaemon.plist");

    template = template
      .Replace("{{SERVICE_NAME}}", GetInstallerDaemonServiceName())
      .Replace("{{INSTALLER_PATH}}", GetUpdaterInstallerPath())
      .Replace("{{SERVER_URI}}", request.ServerUri.ToString())
      .Replace("{{TENANT_ID}}", request.TenantId.ToString());

    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      var lines = template.Split('\n');
      lines = [.. lines.Where(line =>
        !line.Contains("{{INSTANCE_ID}}") &&
        !line.Contains("<string>--instance-id</string>"))];

      template = string.Join("\n", lines);
    }
    else
    {
      template = template.Replace("{{INSTANCE_ID}}", _instanceOptions.Value.InstanceId);
    }

    return template;
  }

  private string GetInstallerDaemonFilePath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchDaemons/app.controlr.agent.installer.plist";
    }

    return $"/Library/LaunchDaemons/app.controlr.agent.installer.{_instanceOptions.Value.InstanceId}.plist";
  }

  private string GetInstallerDaemonServiceName()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "app.controlr.agent.installer";
    }

    return $"app.controlr.agent.installer.{_instanceOptions.Value.InstanceId}";
  }

  private string GetLaunchAgentFilePath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchAgents/app.controlr.desktop.plist";
    }

    return $"/Library/LaunchAgents/app.controlr.desktop.{_instanceOptions.Value.InstanceId}.plist";
  }

  private async Task<string> GetLaunchDaemonFile()
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerMac).Assembly,
      "LaunchDaemon.plist");

    var bundleExtractDir = FilesystemPathProvider.GetDotnetExtractDirectory();

    template = template
      .Replace("{{SERVICE_NAME}}", GetAgentServiceName())
      .Replace("{{AGENT_PATH}}", FilesystemPathProvider.GetAgentExecutablePath())
      .Replace("{{DOTNET_BUNDLE_EXTRACT_BASE_DIR}}", bundleExtractDir);

    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      // Remove lines containing {{INSTANCE_ID}} and <string>-i</string>
      var lines = template.Split('\n');
      lines = [.. lines.Where(line =>
        !line.Contains("{{INSTANCE_ID}}") &&
        !line.Contains("<string>-i</string>"))];

      template = string.Join("\n", lines);
    }
    else
    {
      template = template.Replace("{{INSTANCE_ID}}", _instanceOptions.Value.InstanceId);
    }
    return template;
  }

  private string GetLaunchDaemonFilePath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchDaemons/app.controlr.agent.plist";
    }

    return $"/Library/LaunchDaemons/app.controlr.agent.{_instanceOptions.Value.InstanceId}.plist";
  }

  private string GetRepairStageDirectory()
  {
    return _fileSystem.JoinPaths('/', PathConstants.MacApplicationsDirectory, $".controlr-desktop-repair-{Guid.NewGuid():N}");
  }

  private string GetSourceAgentPath(string sourceAppBundlePath)
  {
    return FilesystemPathProvider.GetSourceAgentPath(sourceAppBundlePath);
  }

  private string GetTempExtractDirectory()
  {
    var instanceSegment = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? AppConstants.DefaultInstallDirectoryName
      : _instanceOptions.Value.InstanceId;

    return Path.Combine(MacInstallerTempDirectory, instanceSegment, "bundle");
  }

  private string GetUpdaterInstallerPath()
  {
    var instanceSegment = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? AppConstants.DefaultInstallDirectoryName
      : _instanceOptions.Value.InstanceId;

    return _fileSystem.JoinPaths(
      '/',
      MacInstallerTempDirectory,
      instanceSegment,
      AppConstants.GetInstallerFileName(SystemPlatform.MacOs));
  }

  private void SetAgentPermissions(string installedAgentPath)
  {
    if (!OperatingSystem.IsMacOS())
    {
      throw new PlatformNotSupportedException();
    }

    _fileSystem.SetUnixFileMode(
      installedAgentPath,
      UnixFileMode.UserRead |
      UnixFileMode.UserWrite |
      UnixFileMode.UserExecute |
      UnixFileMode.GroupRead |
      UnixFileMode.GroupExecute |
      UnixFileMode.OtherRead |
      UnixFileMode.OtherExecute);
  }

  private void SetExecutablePermissions(string appBundlePath)
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
      Path.Combine(appBundlePath, "Contents", "MacOS", "ControlR.DesktopClient"),
      Path.Combine(appBundlePath, "Contents", "Library", "LaunchServices", "ControlR.Agent")
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

  private async Task StopInstallerDaemonService()
  {
    try
    {
      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
        UseShellExecute = true,
        WorkingDirectory = "/tmp",
        Arguments = $"launchctl bootout system/{GetInstallerDaemonServiceName()}"
      };

      await ProcessManager.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));
    }
    catch
    {
      // The installer daemon may not already be loaded.
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

  private void ValidateRepairedAppBundle(string stagedAppBundlePath)
  {
    if (!_fileSystem.DirectoryExists(stagedAppBundlePath))
    {
      throw new DirectoryNotFoundException($"The repair bundle app was not found at '{stagedAppBundlePath}'.");
    }

    var desktopExecutablePath = _fileSystem.JoinPaths('/', stagedAppBundlePath, PathConstants.MacDesktopExecutableRelativePath);
    if (!_fileSystem.FileExists(desktopExecutablePath))
    {
      throw new FileNotFoundException("The repair bundle does not contain the desktop client executable.", desktopExecutablePath);
    }
  }

  private async Task WriteFileIfChanged(string filePath, string content)
  {
    if (_fileSystem.FileExists(filePath))
    {
      var existingContent = await _fileSystem.ReadAllTextAsync(filePath);
      if (existingContent.Trim() == content)
      {
        _logger.LogInformation("File {FilePath} already exists with the same content. Skipping write.", filePath);
        return;
      }
    }

    _logger.LogInformation("Writing file {FilePath}.", filePath);
    await _fileSystem.WriteAllTextAsync(filePath, content);
  }
}
