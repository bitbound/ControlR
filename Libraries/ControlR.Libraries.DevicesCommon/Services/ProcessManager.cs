using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Exceptions;
using System.Diagnostics;

namespace ControlR.Libraries.DevicesCommon.Services;

public interface IProcessManager
{
    Process GetCurrentProcess();

    Process GetProcessById(int processId);

    Process[] GetProcesses();

    Process[] GetProcessesByName(string processName);

    Task<Result<string>> GetProcessOutput(string command, string arguments, int timeoutMs = 10_000);

    Process? LaunchUri(Uri uri);

    Process Start(string fileName);

    Process Start(string fileName, string arguments);

    Process? Start(string fileName, string arguments, bool useShellExec);

    Process? Start(ProcessStartInfo startInfo);

    Task StartAndWaitForExit(ProcessStartInfo startInfo, TimeSpan timeout);

    Task StartAndWaitForExit(string fileName, string arguments, bool useShellExec, TimeSpan timeout);
    Task StartAndWaitForExit(string fileName, string arguments, bool useShellExec, CancellationToken cancellationToken);
}

public class ProcessManager : IProcessManager
{
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
        Guard.IsNotNull(startInfo);
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
        using var process = Process.Start(startInfo);
        Guard.IsNotNull(process);

        using var cts = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(cts.Token);

        if (process.ExitCode != 0)
        {
            throw new ProcessStatusException(process.ExitCode);
        }
    }

    public async Task StartAndWaitForExit(string fileName, string arguments, bool useShellExec, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        await StartAndWaitForExit(fileName, arguments, useShellExec, cts.Token);
    }

    public async Task StartAndWaitForExit(string fileName, string arguments, bool useShellExec, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo()
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = useShellExec,
        };

        using var process = Process.Start(psi);
        Guard.IsNotNull(process);
        await process.WaitForExitAsync(cancellationToken);
    }
}