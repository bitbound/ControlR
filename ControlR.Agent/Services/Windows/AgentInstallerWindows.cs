using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class AgentInstallerWindows(
    IHostApplicationLifetime lifetime,
    IProcessInvoker processes,
    IFileSystem fileSystem,
    IEnvironmentHelper environmentHelper,
    IDownloadsApi downloadsApi,
    ILogger<AgentInstallerWindows> logger) : AgentInstallerBase(fileSystem, downloadsApi, logger), IAgentInstaller
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly IEnvironmentHelper _environmentHelper = environmentHelper;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly string _installDir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Program Files", "ControlR");
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ILogger<AgentInstallerWindows> _logger = logger;
    private readonly IProcessInvoker _processes = processes;
    private readonly string _serviceName = "ControlR.Agent";

    public async Task Install(string? authorizedPublicKey = null, int? vncPort = null, bool? autoInstallVnc = null)
    {
        if (!await _installLock.WaitAsync(0))
        {
            _logger.LogWarning("Installer lock already acquired.  Aborting.");
            return;
        }

        try
        {
            _logger.LogInformation("Install started.");

            if (!CheckIsAdministrator())
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

            var exePath = _environmentHelper.StartupExePath;
            var fileName = Path.GetFileName(exePath);
            var targetPath = Path.Combine(_installDir, AppConstants.AgentFileName);
            _fileSystem.CreateDirectory(_installDir);

            try
            {
                await TryHelper.Retry(
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

            await UpdateAppSettings(_installDir, authorizedPublicKey, vncPort, autoInstallVnc);
            await WriteEtag(_installDir);

            var createString = $"sc.exe create {_serviceName} binPath= \"\\\"{targetPath}\\\" run\" start= auto";
            var configString = $"sc.exe failure \"{_serviceName}\" reset= 5 actions= restart/5000";
            var startString = $"sc.exe start {_serviceName}";

            var result = await _processes.GetProcessOutput("cmd.exe", $"/c {createString} & {configString} & {startString}");

            if (!result.IsSuccess)
            {
                _logger.LogResult(result);
                return;
            }

            // Set Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
            var subkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
            subkey?.SetValue("SoftwareSASGeneration", "3", Microsoft.Win32.RegistryValueKind.DWord);

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

            if (!CheckIsAdministrator())
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

            var deleteResult = await _processes.GetProcessOutput("cmd.exe", $"/c sc.exe delete {_serviceName}");
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
                    _fileSystem.DeleteDirectory(_installDir, true);
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

            GetRegistryBaseKey().DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR", false);

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

    private static bool CheckIsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
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
        var exePath = Path.Combine(_installDir, AppConstants.AgentFileName);
        var fileName = Path.GetFileName(exePath);
        var version = FileVersionInfo.GetVersionInfo(exePath);
        var baseKey = GetRegistryBaseKey();

        var controlrKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ControlR", true);
        controlrKey.SetValue("DisplayIcon", Path.Combine(_installDir, fileName));
        controlrKey.SetValue("DisplayName", "ControlR Agent");
        controlrKey.SetValue("DisplayVersion", version.FileVersion ?? "0.0.0");
        controlrKey.SetValue("InstallDate", DateTime.Now.ToShortDateString());
        controlrKey.SetValue("Publisher", "Jared Goodwin");
        controlrKey.SetValue("VersionMajor", $"{version.FileMajorPart}", RegistryValueKind.DWord);
        controlrKey.SetValue("VersionMinor", $"{version.FileMinorPart}", RegistryValueKind.DWord);
        controlrKey.SetValue("UninstallString", Path.Combine(_installDir, $"{fileName} uninstall"));
        controlrKey.SetValue("QuietUninstallString", Path.Combine(_installDir, $"{fileName} uninstall"));
    }

    private bool IsRunningFromAppDir()
    {
        var exePath = _environmentHelper.StartupExePath;
        var appDir = _environmentHelper.StartupDirectory;

        if (!string.Equals(appDir, _installDir, StringComparison.OrdinalIgnoreCase))
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
            using var existingService = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == _serviceName);
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
                proc.Kill();
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