using ControlR.Agent.Shared.Constants;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.Branding;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Services.Encryption;

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
  IEd25519KeyProvider keyProvider,
  ILegacyPathMigrationUnix legacyPathMigration,
  ILogger<AgentInstallerMac> logger)
  : AgentInstallerBase(fileSystem, fileSystemPathProvider, controlrApi, deviceDataGenerator, optionsAccessor, processManager, systemEnvironment, appOptions, logger, keyProvider), IAgentInstaller
{
  private static readonly SemaphoreSlim _installLock = new(1, 1);
  private static readonly string _macInstallerTempDirectory = $"/tmp/{BrandingConstants.UpdaterTempDirectoryName}";

  private readonly IEmbeddedResourceAccessor _embeddedResourceAccessor = embeddedResourceAccessor;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILegacyPathMigrationUnix _legacyPathMigration = legacyPathMigration;
  private readonly ILogger<AgentInstallerMac> _logger = logger;
  private readonly IServiceControl _serviceControl = serviceControl;

  public async Task<Result<AgentInstallOutcome>> Install(AgentInstallRequest request)
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting.");
      return Result.Fail<AgentInstallOutcome>("The installer is already running.");
    }

    // Set once this install has taken over the plist files of an install at another path, which is the only
    // state a later failure has to undo. Declared out here so the catch below can see it too.
    IReadOnlyDictionary<string, string>? previousPlists = null;

    try
    {
      _logger.LogInformation("Install started.");

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Install command must be run with sudo.");
        return Result.Fail<AgentInstallOutcome>("Install command must be run with sudo.");
      }

      // This install boots out the agent that spawned it, and launchd tears down the whole process
      // group of a dying job. Leaving that group before any of that starts is what lets this process
      // survive. The result is unread because EPERM is expected and correct when this process is
      // already a session leader, which is the case for a manual run from a terminal.
      _ = Libc.Setsid();

      var appBundleInstallPath = GetInstalledAppBundlePath();
      var tempExtractDirectory = GetTempExtractDirectory();
      var installedAgentPath = GetInstalledAgentPath();
      var installedAgentDirectory = Path.GetDirectoryName(installedAgentPath)
        ?? throw new DirectoryNotFoundException("Unable to determine the agent install directory.");
      var bundleStateDirectory = GetBundleStateDirectory();

      // TODO: Temporary migration code for v0.31.x
      var relocation = _legacyPathMigration.Plan();
      if (relocation is not null && !_legacyPathMigration.TryStageSettings(relocation).IsSuccess)
      {
        return Result.Fail<AgentInstallOutcome>("Could not carry this install's settings to the new path.");
      }

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
        var tempAppBundlePath = Path.Combine(tempExtractDirectory, $"{BrandingConstants.MacAppBundleBaseName}.app");
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

      // TODO: Temporary migration code for v0.31.x
      // The plists name the install directory, so rewriting them is what moves the job. Until this install
      // has confirmed the relocated job is running, the previous contents are what puts the old install
      // back in play if a later step fails.
      if (relocation is not null)
      {
        previousPlists = _legacyPathMigration.ReadServiceDefinitionFiles(agentPlistPath, desktopPlistPath);
      }

      _logger.LogInformation("Writing plist files.");
      await WriteFileIfChanged(agentPlistPath, agentPlistFile);
      await WriteFileIfChanged(desktopPlistPath, desktopPlistFile);
      RemoveInstallerDaemonFile();
      await UpdateAppSettings(request.ServerUri, request.TenantId, request.DeviceId);

      var createResult = await CreateDeviceOnServer(request.InstallerKeyId, request.InstallerKeySecret, request.TagIds, request.CustomerId);
      if (!createResult.IsSuccess)
      {
        await RestoreRelocatedPlists(previousPlists);
        return createResult.ToResult(AgentInstallOutcome.Installed);
      }

      await WriteBundleHashFile(request.BundleSha256);

      // Only one agent may hold the device's connection on the server, so the replaced install stops
      // before this one starts. Starting beside an install that could not be stopped would leave both
      // signing as the same device, so fail and let the caller put the replaced install back.
      var stopPreviousBrandResult = await StopPreviousBrandService(
        request.PreviousPublisher,
        request.PreviousBrandName,
        request.PreviousInstanceId);
      if (!stopPreviousBrandResult.IsSuccess)
      {
        await RestoreRelocatedPlists(previousPlists);
        return Result.Fail<AgentInstallOutcome>($"Failed to stop the {request.PreviousBrandName} install being replaced.");
      }

      try
      {
        await _serviceControl.StartAgentService(throwOnFailure: true);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "The {BrandName} service did not start.", BrandingConstants.BrandName);
        await RestoreRelocatedPlists(previousPlists);
        return Result.Fail<AgentInstallOutcome>("Failed to start the agent service after installation.");
      }

      await _serviceControl.StartDesktopClientService(throwOnFailure: false);

      if (relocation is not null)
      {
        // TODO: Temporary migration code for v0.31.x
        // Only now, with the relocated service confirmed started. Until this point the old install is
        // complete on disk, which is what lets a failure at any earlier step be recovered by starting it.
        _legacyPathMigration.RemoveRelocatedInstall(relocation);
      }

      _logger.LogInformation("Installer finished.");
      return Result.Ok(AgentInstallOutcome.Installed);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, $"Error while installing the {BrandingConstants.BrandName} service.");
      await RestoreRelocatedPlists(previousPlists);
      return Result.Fail<AgentInstallOutcome>(ex);
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

      _logger.LogInformation("Booting out services.");
      await _serviceControl.StopDesktopClientService(throwOnFailure: false);
      await _serviceControl.StopAgentService(throwOnFailure: false);

      if (_fileSystem.FileExists(serviceFilePath))
      {
        _fileSystem.DeleteFile(serviceFilePath);
      }

      if (_fileSystem.FileExists(desktopFilePath))
      {
        _fileSystem.DeleteFile(desktopFilePath);
      }

      RemoveInstallerDaemonFile();

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
      _logger.LogError(ex, $"Error while uninstalling the {BrandingConstants.BrandName} service.");
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
      ? $"{BrandingConstants.MacServicePrefix}.agent"
      : $"{BrandingConstants.MacServicePrefix}.agent.{_instanceOptions.Value.InstanceId}";
  }

  private string GetBundleStateDirectory()
  {
    return FilesystemPathProvider.GetBundleStateDirectory();
  }

  private string GetDesktopServiceName()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? $"{BrandingConstants.MacServicePrefix}.desktop"
      : $"{BrandingConstants.MacServicePrefix}.desktop.{_instanceOptions.Value.InstanceId}";
  }

  private string GetInstalledAgentPath()
  {
    return FilesystemPathProvider.GetAgentExecutablePath();
  }

  private string GetInstalledAppBundlePath()
  {
    return FilesystemPathProvider.GetMacAppBundlePath();
  }

  private string GetInstallerDaemonFilePath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.installer.plist";
    }

    return $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.installer.{_instanceOptions.Value.InstanceId}.plist";
  }

  private string GetLaunchAgentFilePath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return $"/Library/LaunchAgents/{BrandingConstants.MacServicePrefix}.desktop.plist";
    }

    return $"/Library/LaunchAgents/{BrandingConstants.MacServicePrefix}.desktop.{_instanceOptions.Value.InstanceId}.plist";
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
      return $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.plist";
    }

    return $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.{_instanceOptions.Value.InstanceId}.plist";
  }

  private string GetRepairStageDirectory()
  {
    return _fileSystem.JoinPaths('/', PathConstants.MacApplicationsDirectory, $"{BrandingConstants.RepairStageDirectoryPrefix}{Guid.NewGuid():N}");
  }

  private string GetSourceAgentPath(string sourceAppBundlePath)
  {
    return FilesystemPathProvider.GetSourceAgentPath(sourceAppBundlePath);
  }

  private string GetTempExtractDirectory()
  {
    var instanceSegment = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? AppConstants.DefaultInstanceId
      : _instanceOptions.Value.InstanceId;

    return Path.Combine(_macInstallerTempDirectory, instanceSegment, "bundle");
  }

  /// <summary>
  /// Deletes the installer LaunchDaemon that an earlier build installed. That daemon existed only to run
  /// the installer outside the agent's own job, and the installer now does that by leaving the process
  /// group it was spawned in. The file goes without a bootout, because an install that this very job
  /// launched would be killed by one. A definition with no RunAtLoad and no activation trigger is inert
  /// once its file is gone.
  /// TODO: Temporary migration code for v0.31.x
  /// </summary>
  private void RemoveInstallerDaemonFile()
  {
    var installerDaemonFilePath = GetInstallerDaemonFilePath();
    if (_fileSystem.FileExists(installerDaemonFilePath))
    {
      _logger.LogInformation("Deleting obsolete installer LaunchDaemon at {FilePath}.", installerDaemonFilePath);
      _fileSystem.DeleteFile(installerDaemonFilePath);
    }
  }

  /// <summary>
  /// TODO: Temporary migration code for v0.31.x
  /// </summary>
  /// <remarks>
  /// Puts the plist files back on the install they were taken off, so the recovery the caller runs after a
  /// failed install starts a build that was already running rather than this one, which just failed to.
  /// Nothing happens for an install that stayed where it was, since its plists were never moved. The job is
  /// booted out before these files get replaced, so the file is what the next bootstrap reads and no
  /// reload is needed here.
  /// </remarks>
  private async Task RestoreRelocatedPlists(IReadOnlyDictionary<string, string>? previousPlists)
  {
    if (previousPlists is null)
    {
      return;
    }

    await _legacyPathMigration.RestoreServiceDefinitionFiles(previousPlists);
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
      Path.Combine(appBundlePath, "Contents", "MacOS", BrandingConstants.DesktopClientBaseName),
      Path.Combine(appBundlePath, "Contents", "Library", "LaunchServices", BrandingConstants.AgentBaseName)
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
