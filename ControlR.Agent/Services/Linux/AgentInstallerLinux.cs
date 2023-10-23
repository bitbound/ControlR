using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using ControlR.Devices.Common.Native.Linux;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ControlR.Agent.Services.Linux;

internal class AgentInstallerLinux(
    IHostApplicationLifetime lifetime,
    IFileSystem fileSystem,
    IProcessInvoker processInvoker,
    IEnvironmentHelper environmentHelper,
    IDownloadsApi downloadsApi,
    ILogger<AgentInstallerLinux> logger) : AgentInstallerBase(fileSystem, downloadsApi, logger), IAgentInstaller
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly IEnvironmentHelper _environment = environmentHelper;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly string _installDir = "/usr/local/bin/ControlR";
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ILogger<AgentInstallerLinux> _logger = logger;
    private readonly IProcessInvoker _processInvoker = processInvoker;
    private readonly string _serviceFilePath = "/etc/systemd/system/controlr.agent.service";

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

            if (Libc.geteuid() != 0)
            {
                _logger.LogError("Install command must be run with sudo.");
            }

            await StopProcesses();

            var exePath = _environment.StartupExePath;
            var fileName = Path.GetFileName(exePath);
            var targetPath = Path.Combine(_installDir, AppConstants.AgentFileName);
            _fileSystem.CreateDirectory(_installDir);

            TryHelper.Retry(
                () =>
                {
                    _fileSystem.CopyFile(exePath, targetPath, true);
                }, 5, TimeSpan.FromSeconds(1));

            var serviceFile = GetServiceFile().Trim();

            await _fileSystem.WriteAllTextAsync(_serviceFilePath, serviceFile);
            await UpdateAppSettings(_installDir, authorizedPublicKey, vncPort, autoInstallVnc);
            await WriteEtag(_installDir);

            var psi = new ProcessStartInfo()
            {
                FileName = "sudo",
                Arguments = "systemctl enable controlr.agent.service",
                WorkingDirectory = "/tmp",
                UseShellExecute = true
            };

            _logger.LogInformation("Enabling service.");
            await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

            _logger.LogInformation("Restarting service.");
            psi.Arguments = "systemctl restart controlr.agent.service";
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

            await _processInvoker
                .Start("sudo", "systemctl stop controlr.agent.service")
                .WaitForExitAsync(_lifetime.ApplicationStopping);

            await _processInvoker
                .Start("sudo", "systemctl disable controlr.agent.service")
                .WaitForExitAsync(_lifetime.ApplicationStopping);

            var serviceFilePath = "/etc/systemd/system/controlr.agent.service";
            _fileSystem.DeleteFile(serviceFilePath);

            await _processInvoker
                .Start("sudo", "systemctl daemon-reload")
                .WaitForExitAsync(_lifetime.ApplicationStopping);

            _fileSystem.DeleteDirectory(_installDir, true);

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

    private string GetServiceFile()
    {
        return
            $"[Unit]\n" +
            "Description=ControlR provides zero-trust remote control and administration.\n\n" +
            "[Service]\n" +
            $"WorkingDirectory={_installDir}\n" +
            $"ExecStart={_installDir}/{AppConstants.AgentFileName} run\n" +
            "Restart=always\n" +
            "StartLimitIntervalSec=0\n" +
            "RestartSec=10\n\n" +
            "[Install]\n" +
            "WantedBy=graphical.target";
    }

    private async Task<Result> StopProcesses()
    {
        try
        {
            if (_fileSystem.FileExists(_serviceFilePath))
            {
                _logger.LogInformation("Stopping existing service.");
                var psi = new ProcessStartInfo()
                {
                    FileName = "sudo",
                    Arguments = "systemctl stop controlr.agent.service",
                    WorkingDirectory = "/tmp",
                    UseShellExecute = true
                };

                await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(5));
            }

            _logger.LogInformation("Stopping processes.");
            var procs = _processInvoker
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