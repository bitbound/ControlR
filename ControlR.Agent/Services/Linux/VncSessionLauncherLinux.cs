using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Linux;

[SupportedOSPlatform("linux")]
internal class VncSessionLauncherLinux(
    IProcessInvoker _processes,
    IOptionsMonitor<AppOptions> _appOptions,
    IFileSystem _fileSystem,
    ILogger<VncSessionLauncherLinux> _logger) : IVncSessionLauncher
{
    private readonly SemaphoreSlim _createSessionLock = new(1, 1);

    public async Task<Result<VncSession>> CreateSession(Guid sessionId, string password)
    {
        await _createSessionLock.WaitAsync();

        try
        {
            StopProcesses();

            await InstallVnc();

            await CreatePassword(password);

            _ = _processes.Start("sudo", $"vncserver -depth 24 -geometry 1280x800 -localhost -rfbport {_appOptions.CurrentValue.VncPort}");

            var launchSuccess = await WaitHelper.WaitForAsync(
                () =>
                {
                    return _processes
                        .GetProcesses()
                        .Any(x => x.ProcessName.Equals("Xtightvnc", StringComparison.OrdinalIgnoreCase));
                },
                timeout: TimeSpan.FromSeconds(10),
                pollingMs: 250);

            if (!launchSuccess)
            {
                return Result.Fail<VncSession>("VNC server failed to start.");
            }

            var session = new VncSession(
                sessionId,
                async () =>
                {
                    var pidFiles = _fileSystem
                        .GetFiles("/root/.vnc/")
                        .Where(x => x.EndsWith(".pid"))
                        .ToArray();

                    _logger.LogInformation("Found {PidCount} VNC PID files for cleanup.", pidFiles.Length);

                    foreach (var pid in pidFiles)
                    {
                        var display = pid[pid.IndexOf(':')..pid.IndexOf('.')];
                        _logger.LogInformation("Disposing of VNC session on display {DisplayName}.", display);
                        await _processes.StartAndWaitForExit("sudo", $"/usr/bin/vncserver -kill {display}", true, TimeSpan.FromSeconds(5));
                    }
                });

            return Result.Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating VNC session.");
            return Result.Fail<VncSession>("An error occurred while VNC control.");
        }
        finally
        {
            _createSessionLock.Release();
        }
    }

    private async Task CreatePassword(string password)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await _processes.Start("sudo", "mkdir -p /root/.vnc/").WaitForExitAsync();
        var args = $"/bin/bash -c \"echo {password} | vncpasswd -f > /root/.vnc/passwd\"";
        await _processes.Start("sudo", args).WaitForExitAsync(cts.Token);
        await _processes.Start("sudo", "chmod 600 /root/.vnc/passwd").WaitForExitAsync(cts.Token);
    }

    private async Task InstallVnc()
    {
        if (_fileSystem.FileExists("/usr/bin/tightvncserver"))
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await _processes
            .Start("sudo", "apt update && apt install -y tightvncserver")
            .WaitForExitAsync(cts.Token);
    }

    private void StopProcesses()
    {
        foreach (var proc in _processes.GetProcessesByName("tightvncserver"))
        {
            try
            {
                proc.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop existing tightvncserver process.");
            }
        }
    }
}