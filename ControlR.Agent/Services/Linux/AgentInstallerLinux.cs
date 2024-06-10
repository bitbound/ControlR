using ControlR.Agent.Interfaces;
using ControlR.Devices.Native.Linux;
using ControlR.Agent.Services.Base;
using ControlR.Libraries.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using ControlR.Agent.Options;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Agent.Services.Linux;

internal class AgentInstallerLinux(
    IHostApplicationLifetime _lifetime,
    IFileSystem _fileSystem,
    IProcessManager _processInvoker,
    IEnvironmentHelper _environmentHelper,
    IRetryer _retryer,
    ISettingsProvider _settingsProvider,
    IOptionsMonitor<AgentAppOptions> _appOptions,
    IOptions<InstanceOptions> _instanceOptions,
    ILogger<AgentInstallerLinux> _logger) : AgentInstallerBase(_fileSystem, _settingsProvider, _appOptions, _logger), IAgentInstaller
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly IEnvironmentHelper _environment = _environmentHelper;
    private readonly IFileSystem _fileSystem = _fileSystem;
    private readonly IHostApplicationLifetime _lifetime = _lifetime;
    private readonly ILogger<AgentInstallerLinux> _logger = _logger;
    private readonly IProcessManager _processInvoker = _processInvoker;

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

            if (Libc.geteuid() != 0)
            {
                _logger.LogError("Install command must be run with sudo.");
            }

            TryClearDotnetExtractDir();

            var installDir = GetInstallDirectory();

            var exePath = _environment.StartupExePath;
            var fileName = Path.GetFileName(exePath);
            var targetPath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environment.Platform));
            _fileSystem.CreateDirectory(installDir);

            if (_fileSystem.FileExists(targetPath))
            {
                _fileSystem.MoveFile(targetPath, $"{targetPath}.old", true);
            }

            await _retryer.Retry(
                () =>
                {
                    _fileSystem.CopyFile(exePath, targetPath, true);
                    return Task.CompletedTask;
                }, 5, TimeSpan.FromSeconds(1));

            var serviceFile = GetServiceFile().Trim();

            await _fileSystem.WriteAllTextAsync(GetServiceFilePath(), serviceFile);
            await UpdateAppSettings(serverUri, authorizedPublicKey);
            var serviceName = GetServiceName();

            var psi = new ProcessStartInfo()
            {
                FileName = "sudo",
                Arguments = $"systemctl enable {serviceName}",
                WorkingDirectory = "/tmp",
                UseShellExecute = true
            };

            _logger.LogInformation("Enabling service.");
            await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

            _logger.LogInformation("Restarting service.");
            psi.Arguments = $"systemctl restart {serviceName}";
            await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

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

            if (Libc.geteuid() != 0)
            {
                _logger.LogError("Uninstall command must be run with sudo.");
            }

            var serviceName = GetServiceName();

            await _processInvoker
                .Start("sudo", $"systemctl stop {serviceName}")
                .WaitForExitAsync(_lifetime.ApplicationStopping);

            await _processInvoker
                .Start("sudo", $"systemctl disable {serviceName}")
                .WaitForExitAsync(_lifetime.ApplicationStopping);

            _fileSystem.DeleteFile(GetServiceFilePath());

            await _processInvoker
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

    private string GetInstallDirectory()
    {
        var dir = "/usr/local/bin/ControlR";
        if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
        {
            return dir;
        }
        return Path.Combine(dir, _instanceOptions.Value.InstanceId);
    }

    private string GetServiceFile()
    {
        var installDir = GetInstallDirectory();
        var fileName = AppConstants.GetAgentFileName(_environment.Platform);

        var runCommand = "run";
        if (_instanceOptions.Value.InstanceId is string instanceId)
        {
            runCommand += $" -i {instanceId}";
        }

        return
            $"[Unit]\n" +
            "Description=ControlR provides zero-trust remote control and administration.\n\n" +
            "[Service]\n" +
            $"WorkingDirectory={installDir}\n" +
            $"ExecStart={installDir}/{fileName} {runCommand}\n" +
            "Restart=always\n" +
            "StartLimitIntervalSec=0\n" +
            "Environment=DOTNET_ENVIRONMENT=Production\n" +
            "RestartSec=10\n\n" +
            "[Install]\n" +
            "WantedBy=graphical.target";
    }

    private string GetServiceFilePath()
    {
        if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
        {
            return "/etc/systemd/system/controlr.agent.service";
        }

        return $"/etc/systemd/system/controlr.agent-{_instanceOptions.Value.InstanceId}.service";
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