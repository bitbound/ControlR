using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Linux;

[SupportedOSPlatform("linux")]
internal class VncSessionLauncherLinux : IVncSessionLauncher
{
    private readonly IOptionsMonitor<AppOptions> _appOptions;
    private readonly SemaphoreSlim _createSessionLock = new(1, 1);
    private readonly ILogger<VncSessionLauncherLinux> _logger;
    private readonly IProcessInvoker _processes;

    public VncSessionLauncherLinux(
        IProcessInvoker processInvoker,
        IOptionsMonitor<AppOptions> appOptions,
        ILogger<VncSessionLauncherLinux> logger)
    {
        _processes = processInvoker;
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task<Result<VncSession>> CreateSession(Guid sessionId, string password)
    {
        await _createSessionLock.WaitAsync();

        try
        {
            StopProcesses();

            await InstallVnc();

            await CreatePassword(password);

            _ = _processes.Start("sudo", $"vncserver -depth 24 -geometry 1280x800 -localhost -rfbport {_appOptions.CurrentValue.VncPort} :9");

            Process? vncProcess = null;

            await WaitHelper.WaitForAsync(
                () =>
                {
                    vncProcess = _processes
                        .GetProcesses()
                        .Where(x => x.ProcessName.Equals("Xtightvnc", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();
                    return vncProcess is not null;
                },
                TimeSpan.FromSeconds(10));

            if (vncProcess is null)
            {
                return Result.Fail<VncSession>("VNC server failed to start.");
            }

            var session = new VncSession(
                sessionId,
                async () =>
                {
                    await _processes.StartAndWaitForExit("sudo", "/usr/bin/vncserver -kill :9", true, TimeSpan.FromSeconds(5));
                    vncProcess.KillAndDispose();
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