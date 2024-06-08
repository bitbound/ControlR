using ControlR.Agent.Interfaces;
using ControlR.Agent.Options;
using ControlR.Agent.Services.Base;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class AgentInstallerWindows(
    IHostApplicationLifetime _lifetime,
    IProcessManager _processes,
    IFileSystem _fileSystem,
    IEnvironmentHelper _environmentHelper,
    IElevationChecker _elevationChecker,
    IRetryer _retryer,
    ISettingsProvider _settingsProvider,
    IOptionsMonitor<AgentAppOptions> _appOptions,
    IRegistryAccessor _registryAccessor,
    IOptions<InstanceOptions> _instanceOptions,
    ILogger<AgentInstallerWindows> _logger) : AgentInstallerBase(_fileSystem, _settingsProvider, _appOptions, _logger), IAgentInstaller
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly IElevationChecker _elevationChecker = _elevationChecker;
    private readonly IEnvironmentHelper _environmentHelper = _environmentHelper;
    private readonly IFileSystem _fileSystem = _fileSystem;
    private readonly IHostApplicationLifetime _lifetime = _lifetime;
    private readonly ILogger<AgentInstallerWindows> _logger = _logger;
    private readonly IProcessManager _processes = _processes;

    public async Task Install(Uri? serverUri = null, string? authorizedPublicKey = null)
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
                _logger.LogError("Install command must be run as administrator.");
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

            var installDir = GetInstallDirectory();
            var exePath = _environmentHelper.StartupExePath;
            var fileName = Path.GetFileName(exePath);
            var targetPath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environmentHelper.Platform));
            _fileSystem.CreateDirectory(installDir);

            try
            {
                await _retryer.Retry(
                    () =>
                    {
                        _logger.LogInformation("Copying {source} to {dest}.", exePath, targetPath);
                        _fileSystem.CopyFile(exePath, targetPath, true);
                        return Task.CompletedTask;
                    },
                    tryCount: 5,
                    retryDelay: TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to copy app to install directory.  Aborting.");
                return;
            }

            await UpdateAppSettings(serverUri, authorizedPublicKey);

            var serviceName = GetServiceName();

            var subcommand = "run";
            if (_instanceOptions.Value.InstanceId is string instanceId)
            {
                subcommand += $" -i {instanceId}";
            }

            var createString = $"sc.exe create \"{serviceName}\" binPath= \"\\\"{targetPath}\\\" {subcommand}\" start= auto";
            var configString = $"sc.exe failure \"{serviceName}\" reset= 5 actions= restart/5000";
            var startString = $"sc.exe start \"{serviceName}\"";

            var result = await _processes.GetProcessOutput("cmd.exe", $"/c {createString} & {configString} & {startString}");

            if (!result.IsSuccess)
            {
                _logger.LogResult(result);
                return;
            }

            _registryAccessor.EnableSoftwareSas();

            _logger.LogInformation("Creating uninstall registry key.");
            CreateUninstallKey();

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

            if (!_elevationChecker.IsElevated())
            {
                _logger.LogError("Uninstall command must be run as administrator.");
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
                _logger.LogError("{msg}", deleteResult.Reason);
                return;
            }

            for (var i = 0; i <= 5; i++)
            {
                try
                {
                    if (i == 5)
                    {
                        _logger.LogWarning("Unable to delete installation directory.  Continuing.");
                        break;
                    }
                    _fileSystem.DeleteDirectory(GetInstallDirectory(), true);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete install directory.  Retrying in a moment.");
                    await Task.Delay(3_000);
                }
            }

            // Remove Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
            var subkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
            subkey?.DeleteValue("SoftwareSASGeneration", false);

            GetRegistryBaseKey().DeleteSubKeyTree(GetUninstallKeyPath(), false);

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

    private static RegistryKey GetRegistryBaseKey()
    {
        if (Environment.Is64BitOperatingSystem)
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }
        else
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        }
    }

    private void CreateUninstallKey()
    {
        var installDir = GetInstallDirectory();

        var displayName = "ControlR Agent";
        var exePath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environmentHelper.Platform));
        var fileName = Path.GetFileName(exePath);
        var version = FileVersionInfo.GetVersionInfo(exePath);
        var uninstallCommand = Path.Combine(installDir, $"{fileName} uninstall");

        if (_instanceOptions.Value.InstanceId is { } instanceId)
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
        if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
        {
            return dir;
        }

        return Path.Combine(dir, _instanceOptions.Value.InstanceId);
    }

    private string GetServiceName()
    {
        if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
        {
            return "ControlR.Agent";
        }
        return $"ControlR.Agent ({_instanceOptions.Value.InstanceId})";
    }
    private string GetUninstallKeyPath()
    {
        if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
        {
            return @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR";
        }
        return $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR ({_instanceOptions.Value.InstanceId})";
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

        _logger.LogInformation(
            "Installer is being run from the app directory.  " +
            "Copying to temp directory and relaunching with args: {args}.",
            args);

        var dest = Path.Combine(Path.GetTempPath(), $"ControlR_{Guid.NewGuid()}.exe");

        _fileSystem.CopyFile(exePath, dest, true);

        var psi = new ProcessStartInfo()
        {
            FileName = dest,
            Arguments = args,
            UseShellExecute = true
        };
        _processes.Start(psi);
        return true;
    }

    private Result StopProcesses()
    {
        try
        {
            using var existingService = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == GetServiceName());
            if (existingService is not null)
            {
                _logger.LogInformation("Existing service found.  CanStop value: {value}", existingService.CanStop);
            }
            else
            {
                _logger.LogInformation("No existing service found.");
            }
            if (existingService?.CanStop == true)
            {
                _logger.LogInformation("Stopping service.");
                existingService.Stop();
                existingService.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }

            var procs = _processes
                .GetProcessesByName("ControlR.Agent")
                .Where(x => x.Id != Environment.ProcessId);

            foreach (var proc in procs)
            {
                try
                {
                    proc.Kill();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to kill agent process with ID {AgentProcessId}.", proc.Id);
                }
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping service and processes.");
            return Result.Fail(ex);
        }
    }
}