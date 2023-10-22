using ControlR.Devices.Common.Native.Windows;
using ControlR.Shared;
using ControlR.Shared.Enums;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ControlR.Devices.Common.Services;

public interface IProcessInvoker
{
    Process GetCurrentProcess();

    Process GetProcessById(int processId);

    Process[] GetProcesses();

    Process[] GetProcessesByName(string processName);

    Task<Result<string>> GetProcessOutput(string command, string arguments, int timeoutMs = 10_000);

    Task<Result> LaunchRemoteControl(string serverUrl, Guid requestId, string requesterConnectionId);

    Process? LaunchUri(Uri uri);

    Process Start(string fileName);

    Process Start(string fileName, string arguments);

    Process? Start(string fileName, string arguments, bool useShellExec);

    Process? Start(ProcessStartInfo startInfo);

    Task StartAndWaitForExit(ProcessStartInfo startInfo, TimeSpan timeout);
}

public class ProcessInvoker(ILogger<ProcessInvoker> logger) : IProcessInvoker
{
    private readonly ILogger<ProcessInvoker> _logger = logger;

    public Process GetCurrentProcess()
    {
        return Process.GetCurrentProcess();
    }

    public Process GetProcessById(int processId)
    {
        return Process.GetProcessById(processId);
    }

    public Process[] GetProcesses()
    {
        return Process.GetProcesses();
    }

    public Process[] GetProcessesByName(string processName)
    {
        return Process.GetProcessesByName(processName);
    }

    public async Task<Result<string>> GetProcessOutput(string command, string arguments, int timeoutMs = 10_000)
    {
        try
        {
            var psi = new ProcessStartInfo(command, arguments)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            var proc = Process.Start(psi);

            if (proc is null)
            {
                return Result.Fail<string>("Process failed to start.");
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            await proc.WaitForExitAsync(cts.Token);

            var output = await proc.StandardOutput.ReadToEndAsync();
            return Result.Ok(output);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<string>($"Timed out while waiting for command to finish.  " +
                $"Command: {command}.  Arguments: {arguments}");
        }
        catch (Exception ex)
        {
            return Result.Fail<string>(ex);
        }
    }

    public async Task<Result> LaunchRemoteControl(string serverUrl, Guid requestId, string requesterConnectionId)
    {
        try
        {
            switch (EnvironmentHelper.Instance.Platform)
            {
                case SystemPlatform.Unknown:
                case SystemPlatform.MacOS:
                case SystemPlatform.MacCatalyst:
                case SystemPlatform.Browser:
                default:
                    break;

                case SystemPlatform.Windows:
                    {
                        var result = LaunchDesktopStreamerWindows(serverUrl, requestId, requesterConnectionId);
                        if (!result.IsSuccess)
                        {
                            if (result.Exception is not null)
                            {
                                _logger.LogError(result.Exception, "Error while starting desktop app.");
                            }
                            _logger.LogError("{msg}", result.Reason ?? "Desktop app failed to start.");
                        }
                        break;
                    }
                case SystemPlatform.Linux:
                    await LaunchDesktopStreamerLinux(serverUrl, requestId, requesterConnectionId);
                    break;
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching desktop streamer.");
            return Result.Fail(ex);
        }
    }

    public Process? LaunchUri(Uri uri)
    {
        var psi = new ProcessStartInfo()
        {
            FileName = $"{uri}",
            UseShellExecute = true
        };
        return Process.Start(psi);
    }

    public Process Start(string fileName)
    {
        return Process.Start(fileName);
    }

    public Process Start(string fileName, string arguments)
    {
        return Process.Start(fileName, arguments);
    }

    public Process? Start(ProcessStartInfo startInfo)
    {
        if (startInfo is null)
        {
            throw new ArgumentNullException(nameof(startInfo));
        }
        return Process.Start(startInfo);
    }

    public Process? Start(string fileName, string arguments, bool useShellExec)
    {
        var psi = new ProcessStartInfo()
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = useShellExec
        };
        return Process.Start(psi);
    }

    public async Task StartAndWaitForExit(ProcessStartInfo startInfo, TimeSpan timeout)
    {
        var process = Process.Start(startInfo);
        Guard.IsNotNull(process);

        using var cts = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(cts.Token);
    }

    private Task LaunchDesktopStreamerLinux(string serverUrl, Guid requestId, string requesterConnectionId)
    {
        throw new NotImplementedException();
    }

    private Result LaunchDesktopStreamerWindows(string serverUrl, Guid requestId, string requesterConnectionId)
    {
        try
        {
            var filename = "ScreenR.exe";
            var arguments = $"start -s {serverUrl} -i {requestId}";

            if (Process.GetCurrentProcess().SessionId == 0)
            {
                var result = Win32.CreateInteractiveSystemProcess(
                    $"{filename} {arguments}",
                    -1,
                    false,
                    "Default",
                    true,
                    out _);

                if (!result)
                {
                    return Result.Fail("Desktp app failed to start.");
                }
            }
            else
            {
                Process.Start(filename, arguments);
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }
}