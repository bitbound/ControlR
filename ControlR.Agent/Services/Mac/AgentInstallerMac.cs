using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using ControlR.Devices.Common.Native.Linux;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Exceptions;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ControlR.Agent.Services.Mac;

internal class AgentInstallerMac(
    IHostApplicationLifetime _lifetime,
    IFileSystem _fileSystem,
    IProcessManager _processInvoker,
    IEnvironmentHelper _environmentHelper,
    IRetryer _retryer,
    ISettingsProvider _settingsProvider,
    IOptionsMonitor<AgentAppOptions> _appOptions,
    ILogger<AgentInstallerMac> _logger) : AgentInstallerBase(_fileSystem, _settingsProvider, _appOptions, _logger), IAgentInstaller
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly IEnvironmentHelper _environment = _environmentHelper;
    private readonly IFileSystem _fileSystem = _fileSystem;
    private readonly string _installDir = "/usr/local/bin/ControlR";
    private readonly IHostApplicationLifetime _lifetime = _lifetime;
    private readonly ILogger<AgentInstallerMac> _logger = _logger;
    private readonly IProcessManager _processInvoker = _processInvoker;
    private readonly string _serviceFilePath = "/Library/LaunchDaemons/controlr-agent.plist";

    public async Task Install(Uri? serverUri = null, string? authorizedPublicKey = null, int? vncPort = null, bool? autoRunVnc = null)
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

            _logger.LogInformation("Writing service file.");
            await _fileSystem.WriteAllTextAsync(_serviceFilePath, serviceFile);
            await UpdateAppSettings(serverUri, authorizedPublicKey, vncPort, autoRunVnc);

            var psi = new ProcessStartInfo()
            {
                FileName = "sudo",
                WorkingDirectory = "/tmp",
                UseShellExecute = true
            };

            try
            {
                _logger.LogInformation("Bootstrapping service.");
                psi.Arguments = $"launchctl bootstrap system {_serviceFilePath}";
                await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));
            }
            catch (ProcessStatusException) { }

            _logger.LogInformation("Kickstarting service.");
            psi.Arguments = "launchctl kickstart -k system/dev.jaredg.controlr-agent";
            _ = _processInvoker.Start(psi);

            _logger.LogInformation("Installer finished.");
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

            var psi = new ProcessStartInfo()
            {
                FileName = "sudo",
                Arguments = $"launchctl bootout system {_serviceFilePath}",
                WorkingDirectory = "/tmp",
                UseShellExecute = true
            };

            try
            {
                _logger.LogInformation("Booting out service.");
                psi.Arguments = $"launchctl bootout system {_serviceFilePath}";
                await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));
            }
            catch (ProcessStatusException) { }

            if (_fileSystem.FileExists(_serviceFilePath))
            {
                _fileSystem.DeleteFile(_serviceFilePath);
            }

            if (_fileSystem.DirectoryExists(_installDir))
            {
                _fileSystem.DeleteDirectory(_installDir, true);
            }

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
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            $"<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            $"<plist version=\"1.0\">\n" +
            $"<dict>\n" +
            $"    <key>Label</key>\n" +
            $"    <string>dev.jaredg.controlr-agent</string>\n" +
            $"    <key>KeepAlive</key>\n" +
            $"    <true/>\n" +
            //$"    <key>StandardErrorPath</key>\n" +
            //$"    <string>/var/log/ControlR/plist-err.log</string>\n" +
            //$"    <key>StandardOutPath</key>\n" +
            //$"    <string>/var/log/ControlR/plist-std-log</string> \n" +
            $"    <key>ProgramArguments</key>\n" +
            $"    <array>\n" +
            $"        <string>{_installDir}/ControlR.Agent</string>\n" +
            $"        <string>run</string>\n" +
            $"    </array>\n" +
            $"</dict>\n" +
            $"</plist>";
    }
}