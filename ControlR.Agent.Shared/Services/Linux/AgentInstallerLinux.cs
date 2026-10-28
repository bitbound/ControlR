using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ControlR.Libraries.Shared.Services.Encryption;

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
  IEd25519KeyProvider keyProvider,
  ILinuxDesktopClientProcessLocator processLocator,
  ILogger<AgentInstallerLinux> logger)
  : AgentInstallerBase(fileSystem, fileSystemPathProvider, controlrApi, deviceDataGenerator, optionsAcccessor, processManager, systemEnvironment, appOptions, logger, keyProvider), IAgentInstaller
{
  private const string DesktopClientDirectoryName = "DesktopClient";

  private static readonly SemaphoreSlim _installLock = new(1, 1);

  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IEmbeddedResourceAccessor _embeddedResourceAccessor = embeddedResourceAccessor;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly ILogger<AgentInstallerLinux> _logger = logger;
  private readonly ILinuxDesktopClientProcessLocator _processLocator = processLocator;
  private readonly IServiceControl _serviceControl = serviceControl;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  public async Task<Result<AgentInstallOutcome>> Install(AgentInstallRequest request)
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting.");
      return Result.Fail<AgentInstallOutcome>("The installer is already running.");
    }

    try
    {
      _logger.LogInformation("Install started.");

      if (!_elevationChecker.IsElevated())
      {
        _logger.LogError("Install command must be run with sudo.");
        return Result.Fail<AgentInstallOutcome>("Install command must be run with sudo.");
      }

      // The install is usually handed off from the running agent's own service directory, so this
      // process can start with the install directory as its working directory. Deleting it makes
      // getcwd() fail, and every later relative file name (including "sudo") then throws.
      _systemEnvironment.SetWorkingDirectory(Path.GetTempPath());

      var installDir = GetInstallDirectory();

      var serviceName = GetServiceName();
      var desktopServiceName = GetDesktopServiceName();

      await _serviceControl.StopDesktopClientService(throwOnFailure: false);

      if (_fileSystem.DirectoryExists(installDir))
      {
        await ProcessManager
          .Start("sudo", $"systemctl stop {serviceName}")
          .WaitForExitAsync(_lifetime.ApplicationStopping);

        // The replaced agent may not take its own login screen client down when it stops, because
        // older builds never did. A client left running here is orphaned the moment this directory
        // is replaced. Kill by path so the new install does not start beside a stale client.
        StopDesktopClientProcesses(GetDesktopClientPath(installDir));

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

      var createResult = await CreateDeviceOnServer(request.InstallerKeyId, request.InstallerKeySecret, request.TagIds, request.CustomerId);
      if (!createResult.IsSuccess)
      {
        return createResult.ToResult(AgentInstallOutcome.Installed);
      }

      await WriteBundleHashFile(request.BundleSha256);

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
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

      // Only one agent may hold the device's connection on the server, so the replaced install stops
      // before this one starts. Starting beside an install that could not be stopped would leave both
      // signing as the same device, so fail and let the caller put the replaced install back.
      var stopPreviousBrandResult = await StopPreviousBrandService(request.PreviousBrandName, request.PreviousInstanceId);
      if (!stopPreviousBrandResult.IsSuccess)
      {
        return Result.Fail<AgentInstallOutcome>($"Failed to stop the {request.PreviousBrandName} install being replaced.");
      }

      // The replaced install's agent cannot be trusted to kill its own login screen client. Older
      // builds never did, and a crashed agent could not. Kill the replaced install's clients
      // directly. They are found by the path that install used, which is keyed on its own brand and
      // instance id, so a migration that changes either one still finds them and never touches
      // another install's.
      if (!string.IsNullOrWhiteSpace(request.PreviousBrandName))
      {
        var stopPreviousDesktopResult = await StopPreviousBrandDesktopClients(
          request.PreviousBrandName,
          request.PreviousInstanceId);

        if (!stopPreviousDesktopResult.IsSuccess)
        {
          return Result.Fail<AgentInstallOutcome>($"Failed to stop the {request.PreviousBrandName} desktop client service being replaced.");
        }
      }

      _logger.LogInformation("Starting agent service.");
      try
      {
        // The service control owns the start, because it also confirms the unit reports active rather
        // than trusting that systemd accepted the start job. A Type=simple unit is active as soon as
        // it is forked, so a process that dies immediately still leaves a zero exit behind. The
        // caller uses this verdict to decide whether the install being replaced may be removed.
        await _serviceControl.StartAgentService(throwOnFailure: true);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "The {BrandName} service did not start.", BrandingConstants.BrandName);
        return Result.Fail<AgentInstallOutcome>("Failed to start the agent service after installation.");
      }

      _logger.LogInformation("Starting desktop user services for logged-in users.");
      await _serviceControl.StartDesktopClientService(throwOnFailure: false);

      _logger.LogInformation("Install completed.");
      return Result.Ok(AgentInstallOutcome.Installed);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, $"Error while installing the {BrandingConstants.BrandName} service.");
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

      if (!_elevationChecker.IsElevated())
      {
        _logger.LogError("Desktop repair command must be run with sudo.");
        return;
      }

      var installDir = GetInstallDirectory();
      var desktopServiceName = GetDesktopServiceName();
      var stageDirectory = await PrepareRepairStage(request.BundleZipPath, installDir);

      await _serviceControl.StopDesktopClientService(throwOnFailure: false);

      // Stopping the user service only reaches the clients systemd owns. The greeter client is a raw
      // child of the agent, and moving the directory out from under it orphans it the same way
      // replacing the whole install does.
      StopDesktopClientProcesses(GetDesktopClientPath(installDir));

      ReplaceDesktopClientDirectory(stageDirectory, installDir);

      var desktopServiceFile = (await GetDesktopServiceFile()).Trim();
      _fileSystem.CreateDirectory(Path.GetDirectoryName(GetDesktopServiceFilePath())!);
      await WriteFileIfChanged(GetDesktopServiceFilePath(), desktopServiceFile);
      await WriteBundleHashFile(request.BundleSha256);

      var psi = new ProcessStartInfo
      {
        FileName = "sudo",
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

      if (!_elevationChecker.IsElevated())
      {
        _logger.LogError("Uninstall command must be run with sudo.");
        return;
      }

      var serviceName = GetServiceName();
      var desktopServiceName = GetDesktopServiceName();

      await _serviceControl.StopDesktopClientService(throwOnFailure: false);

      await ProcessManager
        .Start("sudo", $"systemctl stop {serviceName}")
        .WaitForExitAsync(_lifetime.ApplicationStopping);

      // Uninstall leaves no agent behind to reconcile, so take the login screen client down here.
      StopDesktopClientProcesses(GetDesktopClientPath(GetInstallDirectory()));

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
      _logger.LogError(ex, $"Error while uninstalling the {BrandingConstants.BrandName} service.");
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

    var installDir = FilesystemPathProvider.GetAgentInstallDirectory();
    var bundleExtractDir = FilesystemPathProvider.GetDotnetExtractDirectory();

    var instanceArgs = string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId)
      ? ""
      : $" --instance-id {instanceOptions.Value.InstanceId}";

    template = template
      .Replace("{{INSTALL_DIRECTORY}}", installDir)
      .Replace("{{BUNDLE_EXTRACT_DIR}}", bundleExtractDir)
      .Replace("{{INSTANCE_ARGS}}", instanceArgs)
      .Replace("ControlR.DesktopClient", BrandingConstants.DesktopClientBaseName);

    return template;
  }

  /// <summary>
  /// Stops the replaced install's desktop client unit, then kills its client processes. The kill has
  /// to follow a confirmed unit stop: the unit uses Restart=on-failure and a SIGKILL is a failure, so
  /// a unit left active relaunches exactly what the kill just took down. A failed unit stop therefore
  /// aborts before any kill, and the caller treats it like the replaced agent failing to stop.
  /// </summary>
  internal async Task<Result> StopPreviousBrandDesktopClients(string previousBrandName, string? previousInstanceId)
  {
    try
    {
      await _serviceControl.StopPreviousBrandDesktopClientService(
        previousBrandName,
        previousInstanceId,
        throwOnFailure: true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to stop the {BrandName} desktop client service being replaced.", previousBrandName);
      return Result.Fail($"Failed to stop the {previousBrandName} desktop client service being replaced.");
    }

    StopDesktopClientProcesses(GetPreviousBrandDesktopClientPath(previousBrandName, previousInstanceId));

    return Result.Ok();
  }

  internal string GetPreviousBrandDesktopClientPath(string previousBrandName, string? previousInstanceId)
  {
    var installDirectory = FilesystemPathProvider.GetAgentInstallDirectoryFor(previousBrandName, previousInstanceId);

    // The replaced install names its own client for its own brand, which is not this build's name
    // when the migration changed the brand.
    var desktopClientFileName = new BrandNames(previousBrandName).DesktopClientBaseName;

    return Path.Combine(installDirectory, DesktopClientDirectoryName, desktopClientFileName);
  }

  private async Task<string> GetAgentServiceFile()
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerLinux).Assembly,
      "controlr.agent.service");

    var installDir = FilesystemPathProvider.GetAgentInstallDirectory();
    var bundleExtractDir = FilesystemPathProvider.GetDotnetExtractDirectory();

    var instanceArgs = string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId)
      ? ""
      : $" -i {instanceOptions.Value.InstanceId}";

    template = template
      .Replace("{{INSTALL_DIRECTORY}}", installDir)
      .Replace("{{BUNDLE_EXTRACT_DIR}}", bundleExtractDir)
      .Replace("{{INSTANCE_ARGS}}", instanceArgs)
      .Replace("ControlR.Agent", BrandingConstants.AgentBaseName);

    return template;
  }

  private string GetDesktopClientPath(string installDirectory)
  {
    return Path.Combine(installDirectory, DesktopClientDirectoryName, AppConstants.DesktopClientFileName);
  }

  private string GetDesktopServiceFilePath()
  {
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return $"/etc/systemd/user/{BrandingConstants.LinuxDesktopServiceName}";
    }

    return $"/etc/systemd/user/{BrandingConstants.LinuxDesktopServiceName.Replace(".service", "")}-{instanceOptions.Value.InstanceId}.service";
  }

  private string GetDesktopServiceName()
  {
    return Path.GetFileName(GetDesktopServiceFilePath());
  }

  private string GetInstallDirectory()
  {
    return FilesystemPathProvider.GetAgentInstallDirectory();
  }

  private string GetServiceFilePath()
  {
    return FilesystemPathProvider.GetServiceFilePath();
  }

  private string GetServiceName()
  {
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return BrandingConstants.LinuxAgentServiceName;
    }

    return $"{BrandingConstants.LinuxAgentServiceName.Replace(".service", "")}-{instanceOptions.Value.InstanceId}.service";
  }

  private async Task<string> PrepareRepairStage(string bundleZipPath, string installDir)
  {
    var parentDirectory = Path.GetDirectoryName(installDir)
      ?? throw new DirectoryNotFoundException("Unable to determine the install directory parent.");
    var stageDirectory = Path.Combine(parentDirectory, $"{BrandingConstants.RepairStageDirectoryPrefix}{Guid.NewGuid():N}");

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

    var executablePaths = new[]
    {
      FilesystemPathProvider.GetAgentExecutablePath(),
      Path.Combine(installDirectory, "DesktopClient", AppConstants.DesktopClientFileName)
    };

    foreach (var executablePath in executablePaths)
    {
      if (!_fileSystem.FileExists(executablePath))
      {
        continue;
      }

      _fileSystem.SetUnixFileMode(executablePath, executableFileMode);
      _logger.LogDebug("Set executable permissions on {FilePath}", executablePath);
    }
  }

  private void StopDesktopClientProcesses(string desktopExecutablePath)
  {
    foreach (var process in _processLocator.FindByExecutablePath(desktopExecutablePath))
    {
      // Re-check the executable before killing, so a pid recycled since it was found is not killed by
      // mistake. Unlike the greeter reconcile, the owner uid is not a filter here. An install being
      // replaced or removed owns every client under its directory, user sessions included.
      if (_processLocator.GetRunningExecutable(process.ProcessId, desktopExecutablePath) is null)
      {
        continue;
      }

      try
      {
        ProcessManager.GetProcessById(process.ProcessId).KillAndDispose();
        _logger.LogInformation(
          "Killed desktop client process for {ExecutablePath}. PID: {ProcessId}",
          desktopExecutablePath,
          process.ProcessId);
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to kill desktop client process. PID: {ProcessId}", process.ProcessId);
      }
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
