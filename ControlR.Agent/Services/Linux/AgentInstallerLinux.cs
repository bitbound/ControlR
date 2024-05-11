using ControlR.Agent.Interfaces;
using ControlR.Devices.Common.Native.Linux;
using ControlR.Agent.Services.Base;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ControlR.Agent.Services.Linux;

internal class AgentInstallerLinux(
    IHostApplicationLifetime _lifetime,
    IFileSystem _fileSystem,
    IProcessManager _processInvoker,
    IEnvironmentHelper _environmentHelper,
    IRetryer _retryer,
    ISettingsProvider _settingsProvider,
    IOptionsMonitor<AgentAppOptions> _appOptions,
    ILogger<AgentInstallerLinux> _logger) : AgentInstallerBase(_fileSystem, _settingsProvider, _appOptions, _logger), IAgentInstaller
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly IEnvironmentHelper _environment = _environmentHelper;
    private readonly IFileSystem _fileSystem = _fileSystem;
    private readonly string _installDir = "/usr/local/bin/ControlR";
    private readonly IHostApplicationLifetime _lifetime = _lifetime;
    private readonly ILogger<AgentInstallerLinux> _logger = _logger;
    private readonly IProcessManager _processInvoker = _processInvoker;
    private readonly string _serviceFilePath = "/etc/systemd/system/controlr.agent.service";

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

            var exePath = _environment.StartupExePath;
            var fileName = Path.GetFileName(exePath);
            var targetPath = Path.Combine(_installDir, AppConstants.AgentFileName);
            _fileSystem.CreateDirectory(_installDir);

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

            await _fileSystem.WriteAllTextAsync(_serviceFilePath, serviceFile);
            await UpdateAppSettings(serverUri, authorizedPublicKey);

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

            _fileSystem.DeleteFile(_serviceFilePath);

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
            "Environment=DOTNET_ENVIRONMENT=Production\n" +
            "RestartSec=10\n\n" +
            "[Install]\n" +
            "WantedBy=graphical.target";
    }
}