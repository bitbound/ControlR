using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Options;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows")]
internal class AgentInstallerWindows(
  IHostApplicationLifetime lifetime,
  IProcessManager processes,
  ISystemEnvironment environmentHelper,
  IElevationChecker elevationChecker,
  IRetryer retryer,
  IControlrApi controlrApi,
  IDeviceDataGenerator deviceDataGenerator,
  IRegistryAccessor registryAccessor,
  IOptions<InstanceOptions> instanceOptions,
  IFileSystem fileSystem,
  ISettingsProvider settingsProvider,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerWindows> logger)
  : AgentInstallerBase(fileSystem, controlrApi, deviceDataGenerator, settingsProvider, processes, appOptions, logger), IAgentInstaller
{
  private static readonly SemaphoreSlim _installLock = new(1, 1);
  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IRegistryAccessor _registryAccessor = registryAccessor;
  private readonly ISystemEnvironment _environmentHelper = environmentHelper;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly IProcessManager _processes = processes;

  public async Task Install(
    Uri? serverUri = null,
    Guid? tenantId = null,
    string? installerKey = null,
    Guid[]? tags = null)
  {
    if (!await _installLock.WaitAsync(0))
    {
      Logger.LogWarning("Installer lock already acquired.  Aborting.");
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

      Logger.LogInformation("Install started.");

      if (!_environmentHelper.IsDebug && !_elevationChecker.IsElevated())
      {
        Logger.LogError("Install command must be run as administrator.");
        return;
      }

      if (IsRunningFromAppDir())
      {
        return;
      }

      var stopResult = StopAgentService();
      if (!stopResult.IsSuccess)
      {
        Logger.LogError("Failed to stop existing agent service. Aborting installation.");
        return;
      }
      stopResult = StopProcesses();
      if (!stopResult.IsSuccess)
      {
        Logger.LogError("Failed to stop existing agent processes. Aborting installation.");
        return;
      }

      var installDir = GetInstallDirectory();
      var exePath = _environmentHelper.StartupExePath;
      var targetPath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environmentHelper.Platform));
      FileSystem.CreateDirectory(installDir);

      try
      {
        await retryer.Retry(
          () =>
          {
            Logger.LogInformation("Copying {source} to {dest}.", exePath, targetPath);
            FileSystem.CopyFile(exePath, targetPath, true);
            return Task.CompletedTask;
          },
          5,
          TimeSpan.FromSeconds(3));
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Unable to copy app to install directory.  Aborting.");
        return;
      }

      await UpdateAppSettings(serverUri, tenantId);

      var createResult = await CreateDeviceOnServer(installerKey, tags);
      if (!createResult.IsSuccess)
      {
        return;
      }

      var serviceName = GetServiceName();

      var subcommand = "run";
      if (instanceOptions.Value.InstanceId is { } instanceId)
      {
        subcommand += $" -i {instanceId}";
      }

      var createString = $"sc.exe create \"{serviceName}\" binPath= \"\\\"{targetPath}\\\" {subcommand}\" start= auto";
      var configString = $"sc.exe failure \"{serviceName}\" reset= 5 actions= restart/5000";
      var startString = $"sc.exe start \"{serviceName}\"";

      var result = await _processes.GetProcessOutput("cmd.exe", $"/c {createString} & {configString} & {startString}");

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
      _lifetime.StopApplication();
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

      var stopResult = StopProcesses();
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
      _lifetime.StopApplication();
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
    var exePath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environmentHelper.Platform));
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
    controlrKey.SetValue("InstallDate", DateTime.Now.ToShortDateString());
    controlrKey.SetValue("Publisher", "Jared Goodwin");
    controlrKey.SetValue("VersionMajor", $"{version.FileMajorPart}", RegistryValueKind.DWord);
    controlrKey.SetValue("VersionMinor", $"{version.FileMinorPart}", RegistryValueKind.DWord);
    controlrKey.SetValue("UninstallString", uninstallCommand);
    controlrKey.SetValue("QuietUninstallString", uninstallCommand);
  }

  private string GetInstallDirectory()
  {
    var dir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Program Files", "ControlR");
    if (string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId))
    {
      return dir;
    }

    return Path.Combine(dir, instanceOptions.Value.InstanceId);
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
    var exePath = _environmentHelper.StartupExePath;
    var appDir = _environmentHelper.StartupDirectory;

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
        existingService.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
      }
      return Result.Ok();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while stopping agent service.");
      return Result.Fail(ex);
    }
  }
}