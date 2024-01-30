using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using ControlR.Devices.Common.Native.Linux;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Exceptions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ControlR.Agent.Services.Mac;

internal class AgentInstallerMac(
    IHostApplicationLifetime lifetime,
    IFileSystem fileSystem,
    IProcessManager processInvoker,
    IEnvironmentHelper environmentHelper,
    ILogger<AgentInstallerMac> logger) : AgentInstallerBase(fileSystem, logger), IAgentInstaller
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly IEnvironmentHelper _environment = environmentHelper;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly string _installDir = "/usr/local/bin/ControlR";
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ILogger<AgentInstallerMac> _logger = logger;
    private readonly IProcessManager _processInvoker = processInvoker;
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

            await TryHelper.Retry(
                () =>
                {
                    _fileSystem.CopyFile(exePath, targetPath, true);
                    return Task.CompletedTask;
                }, 5, TimeSpan.FromSeconds(1));

            var serviceFile = GetServiceFile().Trim();

            await _fileSystem.WriteAllTextAsync(_serviceFilePath, serviceFile);
            var appOptions = await UpdateAppSettings(_installDir, serverUri, authorizedPublicKey, vncPort, autoRunVnc);

            var psi = new ProcessStartInfo()
            {
                FileName = "sudo",
                Arguments = $"launchctl bootout system {_serviceFilePath}",
                WorkingDirectory = "/tmp",
                UseShellExecute = true
            };

            _logger.LogInformation("Stopping service, if running.");
            try
            {
                await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));
            }
            catch (ProcessStatusException) { }

            _logger.LogInformation("Loading service.");
            psi.Arguments = $"launchctl bootstrap system {_serviceFilePath}";
            await _processInvoker.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

            _logger.LogInformation("Kickstarting service.");
            psi.Arguments = "launchctl kickstart -k system/com.jaredg.controlr-agent";
            _ = _processInvoker.Start(psi);

            _logger.LogInformation("Installer launched.");
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
                .Start("sudo", $"launchctl unload -w {_serviceFilePath}")
                .WaitForExitAsync(_lifetime.ApplicationStopping);

            _fileSystem.DeleteFile(_serviceFilePath);

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
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            $"<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            $"<plist version=\"1.0\">\n" +
            $"<dict>\n" +
            $"    <key>Label</key>\n" +
            $"    <string>com.jaredg.controlr-agent</string>\n" +
            $"    <key>ProgramArguments</key>\n" +
            $"    <array>\n" +
            $"        <string>{_installDir}/ControlR.Agent</string>\n" +
            $"        <string>run</string>\n" +
            $"    </array>\n" +
            $"    <key>KeepAlive</key>\n" +
            $"    <true/>\n" +
            $"</dict>\n" +
            $"</plist>";
    }
}