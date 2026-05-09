using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace ControlR.Agent.Shared.Services.Windows;

[SupportedOSPlatform("windows")]
internal class AgentInstallerWindows(
  IHostApplicationLifetime lifetime,
  IProcessManager processes,
  ISystemEnvironment systemEnvironment,
  IElevationChecker elevationChecker,
  IRetryer retryer,
  IControlrApi controlrApi,
  IDeviceInfoProvider deviceDataGenerator,
  IFileSystemPathProvider fileSystemPathProvider,
  IRegistryAccessor registryAccessor,
  IOptions<InstanceOptions> instanceOptions,
  IFileSystem fileSystem,
  IOptionsAccessor optionsAccessor,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerWindows> logger)
  : AgentInstallerBase(fileSystem, fileSystemPathProvider, controlrApi, deviceDataGenerator, optionsAccessor, processes, systemEnvironment, appOptions, logger), IAgentInstaller
{
  private const string DesktopClientDirectoryName = "DesktopClient";

  private static readonly TimeSpan _desktopProcessExitTimeout = TimeSpan.FromSeconds(15);
  private static readonly SemaphoreSlim _installLock = new(1, 1);

  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly IProcessManager _processes = processes;
  private readonly IRegistryAccessor _registryAccessor = registryAccessor;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  public async Task Install(AgentInstallRequest request)
  {
    if (!await _installLock.WaitAsync(0))
    {
      Logger.LogWarning("Installer lock already acquired.  Aborting.");
      return;
    }

    try
    {
      Logger.LogInformation("Install started.");

      if (!_systemEnvironment.IsDebug && !_elevationChecker.IsElevated())
      {
        Logger.LogError("Install command must be run as administrator.");
        return;
      }

      if (IsRunningFromAppDir())
      {
        return;
      }

      await using var callback = new CallbackDisposableAsync(StartService);

      var installDir = GetInstallDirectory();
      var targetAgentPath = GetAgentPath(installDir, _systemEnvironment.Platform);
      var targetDesktopClientPath = Path.Combine(installDir, "DesktopClient", AppConstants.DesktopClientFileName);

      var stopResult = StopAgentService();
      if (!stopResult.IsSuccess)
      {
        Logger.LogError("Failed to stop existing agent service. Aborting installation.");
        return;
      }
      stopResult = StopProcesses(targetAgentPath, targetDesktopClientPath);
      if (!stopResult.IsSuccess)
      {
        Logger.LogError("Failed to stop existing agent processes. Aborting installation.");
        return;
      }

      try
      {
        await retryer.Retry(
          () =>
          {
            if (FileSystem.DirectoryExists(installDir))
            {
              FileSystem.DeleteDirectory(installDir, true);
            }

            FileSystem.CreateDirectory(installDir);
            Logger.LogInformation("Extracting bundle {BundleZipPath} to {InstallDirectory}.", request.BundleZipPath, installDir);
            return ExtractBundleToInstallDirectory(request.BundleZipPath, installDir);
          },
          5,
          TimeSpan.FromSeconds(3));
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Unable to copy app to install directory.  Aborting.");
        return;
      }

      await UpdateAppSettings(request.ServerUri, request.TenantId, request.DeviceId);

      var createResult = await CreateDeviceOnServer(request.InstallerKeyId, request.InstallerKeySecret, request.TagIds);
      if (!createResult.IsSuccess)
      {
        return;
      }

      await WriteBundleHashFile(request.BundleSha256);

      var subcommand = "run";
      if (instanceOptions.Value.InstanceId is { } instanceId)
      {
        subcommand += $" -i {instanceId}";
      }

      var serviceName = GetServiceName();
      var createString = $"sc.exe create \"{serviceName}\" binPath= \"\\\"{targetAgentPath}\\\" {subcommand}\" start= auto";
      var configString = $"sc.exe failure \"{serviceName}\" reset= 5 actions= restart/5000";

      var result = await _processes.GetProcessOutput("cmd.exe", $"/c {createString} & {configString}");

      if (!result.IsSuccess)
      {
        Logger.LogResult(result);
        return;
      }

      _registryAccessor.SetSoftwareSasGeneration(true);

      Logger.LogInformation("Creating uninstall registry key.");
      CreateUninstallKey();

      Logger.LogInformation("Install completed.");
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while installing the ControlR service.");
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
      Logger.LogWarning("Installer lock already acquired.  Aborting desktop repair.");
      return;
    }

    try
    {
      Logger.LogInformation("Desktop repair started.");

      if (!_systemEnvironment.IsDebug && !_elevationChecker.IsElevated())
      {
        Logger.LogError("Desktop repair command must be run as administrator.");
        return;
      }

      if (IsRunningFromAppDir())
      {
        return;
      }

      var installDir = GetInstallDirectory();
      var desktopClientPath = Path.Combine(installDir, DesktopClientDirectoryName, AppConstants.DesktopClientFileName);
      var stopResult = await StopDesktopClientProcesses(desktopClientPath);
      if (!stopResult.IsSuccess)
      {
        Logger.LogError("Failed to stop desktop client processes. Aborting desktop repair.");
        return;
      }

      var stageDirectory = await PrepareRepairStage(request.BundleZipPath, installDir);
      ReplaceDesktopClientDirectory(stageDirectory, installDir);
      await WriteBundleHashFile(request.BundleSha256);

      Logger.LogInformation("Desktop repair completed.");
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while repairing the desktop client.");
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
      Logger.LogWarning("Installer lock already acquired.  Aborting.");
      return;
    }

    try
    {
      Logger.LogInformation("Uninstall started.");

      if (!_elevationChecker.IsElevated())
      {
        Logger.LogError("Uninstall command must be run as administrator.");
        return;
      }

      if (IsRunningFromAppDir())
      {
        return;
      }

      var installDir = GetInstallDirectory();
      var targetAgentPath = GetAgentPath(installDir, _systemEnvironment.Platform);
      var targetDesktopClientPath = Path.Combine(installDir, "DesktopClient", AppConstants.DesktopClientFileName);

      var stopResult = StopProcesses(targetAgentPath, targetDesktopClientPath);
      if (!stopResult.IsSuccess)
      {
        return;
      }

      var deleteResult = await _processes.GetProcessOutput("cmd.exe", $"/c sc.exe delete \"{GetServiceName()}\"");
      if (!deleteResult.IsSuccess)
      {
        Logger.LogError("{msg}", deleteResult.Reason);
        return;
      }

      for (var i = 0; i <= 5; i++)
      {
        try
        {
          if (i == 5)
          {
            Logger.LogWarning("Unable to delete installation directory.  Continuing.");
            break;
          }

          FileSystem.DeleteDirectory(GetInstallDirectory(), true);
          break;
        }
        catch (Exception ex)
        {
          Logger.LogWarning(ex, "Failed to delete install directory.  Retrying in a moment.");
          await Task.Delay(3_000);
        }
      }

      // Remove Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
      _registryAccessor.SetSoftwareSasGeneration(false);

      GetRegistryBaseKey().DeleteSubKeyTree(GetUninstallKeyPath(), false);

      Logger.LogInformation("Uninstall completed.");
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while uninstalling the ControlR service.");
    }
    finally
    {
      _installLock.Release();
    }
  }

  private static RegistryKey GetRegistryBaseKey()
  {
    return RegistryKey.OpenBaseKey(
      RegistryHive.LocalMachine,
      Environment.Is64BitOperatingSystem
        ? RegistryView.Registry64
        : RegistryView.Registry32);
  }

  private void CreateUninstallKey()
  {
    var installDir = GetInstallDirectory();

    var displayName = "ControlR Agent";
    var exePath = Path.Combine(installDir, AppConstants.GetAgentFileName(_systemEnvironment.Platform));
    var fileName = Path.GetFileName(exePath);
    var version = FileVersionInfo.GetVersionInfo(exePath);
    var uninstallCommand = Path.Combine(installDir, $"{fileName} uninstall");

    if (instanceOptions.Value.InstanceId is { } instanceId)
    {
      displayName += $" ({instanceId})";
      uninstallCommand += $" -i {instanceId}";
    }

    using var baseKey = GetRegistryBaseKey();

    using var controlrKey = baseKey.CreateSubKey(GetUninstallKeyPath(), true);
    controlrKey.SetValue("DisplayIcon", Path.Combine(installDir, fileName));
    controlrKey.SetValue("DisplayName", displayName);
    controlrKey.SetValue("DisplayVersion", version.FileVersion ?? "0.0.0");
    controlrKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
    controlrKey.SetValue("Publisher", "Bitbound");
    controlrKey.SetValue("VersionMajor", $"{version.FileMajorPart}", RegistryValueKind.DWord);
    controlrKey.SetValue("VersionMinor", $"{version.FileMinorPart}", RegistryValueKind.DWord);
    controlrKey.SetValue("UninstallString", uninstallCommand);
    controlrKey.SetValue("QuietUninstallString", uninstallCommand);
  }

  private string GetInstallDirectory()
  {
    var rootDirectory = _systemEnvironment.IsDebug
      ? Path.Combine(Path.GetTempPath(), "ControlR", "Install")
      : Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Program Files", "ControlR");

    return GetInstanceInstallDirectory(rootDirectory, instanceOptions.Value.InstanceId);
  }

  private string GetServiceName()
  {
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return "ControlR.Agent";
    }

    return $"ControlR.Agent ({instanceOptions.Value.InstanceId})";
  }

  private string GetUninstallKeyPath()
  {
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR";
    }

    return $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR ({instanceOptions.Value.InstanceId})";
  }

  private bool IsRunningFromAppDir()
  {
    var exePath = _systemEnvironment.StartupExePath;
    var appDir = _systemEnvironment.StartupDirectory;

    if (!string.Equals(appDir.TrimEnd('\\'), GetInstallDirectory().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var args = Environment.GetCommandLineArgs().Skip(1).StringJoin(" ");

    Logger.LogInformation(
      "Installer is being run from the app directory.  " +
      "Copying to temp directory and relaunching with args: {args}.",
      args);

    var dest = Path.Combine(Path.GetTempPath(), $"ControlR_{Guid.NewGuid()}.exe");

    FileSystem.CopyFile(exePath, dest, true);

    var psi = new ProcessStartInfo
    {
      FileName = dest,
      Arguments = args,
      UseShellExecute = true
    };
    _processes.Start(psi);
    return true;
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
          if (FileSystem.DirectoryExists(stageDirectory))
          {
            FileSystem.DeleteDirectory(stageDirectory, true);
          }

          FileSystem.CreateDirectory(stageDirectory);
          Logger.LogInformation("Extracting repair bundle {BundleZipPath} to {InstallDirectory}.", bundleZipPath, stageDirectory);
          return ExtractBundleToInstallDirectory(bundleZipPath, stageDirectory);
        },
        5,
        TimeSpan.FromSeconds(3));

      var stagedDesktopClientPath = Path.Combine(stageDirectory, DesktopClientDirectoryName, AppConstants.DesktopClientFileName);
      if (!FileSystem.FileExists(stagedDesktopClientPath))
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
      FileSystem.CreateDirectory(installDir);

      if (FileSystem.DirectoryExists(destinationDirectory))
      {
        FileSystem.MoveDirectory(destinationDirectory, backupDirectory);
      }

      FileSystem.MoveDirectory(stagedDirectory, destinationDirectory);

      TryDeleteDirectory(backupDirectory);
    }
    catch
    {
      if (!FileSystem.DirectoryExists(destinationDirectory) && FileSystem.DirectoryExists(backupDirectory))
      {
        FileSystem.MoveDirectory(backupDirectory, destinationDirectory);
      }

      throw;
    }
    finally
    {
      TryDeleteDirectory(stageDirectory);
    }
  }

  private async Task StartService()
  {
    Logger.LogInformation("Starting service.");
    var startResult = await _processes.GetProcessOutput("cmd.exe", $"/c sc.exe start \"{GetServiceName()}\"");
    if (!startResult.IsSuccess)
    {
      Logger.LogError("Failed to start service after installation: {msg}", startResult.Reason);
    }
  }

  private Result StopAgentService()
  {
    try
    {
      using var existingService = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == GetServiceName());
      if (existingService is not null)
      {
        Logger.LogInformation("Existing service found.  CanStop value: {value}", existingService.CanStop);
      }
      else
      {
        Logger.LogInformation("No existing service found.");
      }

      if (existingService?.CanStop == true)
      {
        Logger.LogInformation("Stopping service.");
        existingService.Stop();
        existingService.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
      }
      return Result.Ok();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while stopping agent service.");
      return Result.Fail(ex);
    }
  }

  private async Task<Result> StopDesktopClientProcesses(string targetDesktopClientPath)
  {
    var failures = new List<Exception>();

    try
    {
      var comparison = StringComparison.OrdinalIgnoreCase;
      var procs = ProcessManager
        .GetProcessesByName("ControlR.DesktopClient")
        .Where(x => string.Equals(x.FilePath, targetDesktopClientPath, comparison));

      foreach (var proc in procs)
      {
        try
        {
          proc.Kill();
          await WaitForProcessExit(proc);
        }
        catch (Exception ex)
        {
          failures.Add(ex);
          Logger.LogError(ex, "Failed to kill desktop client process with ID {DesktopClientProcessId}.", proc.Id);
        }
      }

      return failures.Count == 0 ? Result.Ok() : Result.Fail(new AggregateException(failures));
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while stopping desktop client processes.");
      return Result.Fail(ex);
    }
  }

  private void TryDeleteDirectory(string directoryPath)
  {
    try
    {
      if (FileSystem.DirectoryExists(directoryPath))
      {
        FileSystem.DeleteDirectory(directoryPath, true);
      }
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Failed to delete temporary directory {DirectoryPath}.", directoryPath);
    }
  }

  private async Task WaitForProcessExit(IProcess process)
  {
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
    timeoutCts.CancelAfter(_desktopProcessExitTimeout);

    try
    {
      await process.WaitForExitAsync(timeoutCts.Token);
    }
    catch (OperationCanceledException) when (_lifetime.ApplicationStopping.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException ex)
    {
      throw new System.TimeoutException(
        $"Desktop client process {process.Id} did not exit within {_desktopProcessExitTimeout}.",
        ex);
    }
  }
}
