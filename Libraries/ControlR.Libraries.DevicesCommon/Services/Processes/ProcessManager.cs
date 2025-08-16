using System.Diagnostics;
using ControlR.Libraries.Shared.Exceptions;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Libraries.DevicesCommon.Services.Processes;

public interface IProcessManager
{
  IProcess GetCurrentProcess();
  int GetCurrentSessionId();
  IProcess GetProcessById(int processId);

  Task<Result<string>> GetProcessOutput(string command, string arguments, int timeoutMs = 10_000);

  IProcess[] GetProcesses();

  IProcess[] GetProcessesByName(string processName);

  IProcess? LaunchUri(Uri uri);

  IProcess Start(string fileName);

  IProcess Start(string fileName, string arguments);

  IProcess? Start(string fileName, string arguments, bool useShellExec);

  IProcess? Start(ProcessStartInfo startInfo);

  Task<int> StartAndWaitForExit(ProcessStartInfo startInfo, TimeSpan timeout);

  Task<int> StartAndWaitForExit(string fileName, string arguments, bool useShellExec, TimeSpan timeout);
  Task<int> StartAndWaitForExit(string fileName, string arguments, bool useShellExec, CancellationToken cancellationToken);
}

public class ProcessManager : IProcessManager
{
  private int? _currentSessionId;

  public IProcess GetCurrentProcess()
  {
    return new ProcessWrapper(Process.GetCurrentProcess());
  }

  public int GetCurrentSessionId()
  {
    return _currentSessionId ??= Process.GetCurrentProcess().SessionId;
  }

  public IProcess GetProcessById(int processId)
  {
    return new ProcessWrapper(Process.GetProcessById(processId));
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

  public IProcess[] GetProcesses()
  {
    return [.. Process.GetProcesses().Select(p => new ProcessWrapper(p))];
  }

  public IProcess[] GetProcessesByName(string processName)
  {
    return [.. Process.GetProcessesByName(processName).Select(p => new ProcessWrapper(p))];
  }

  public IProcess? LaunchUri(Uri uri)
  {
    var psi = new ProcessStartInfo()
    {
      FileName = $"{uri}",
      UseShellExecute = true
    };
    var process = Process.Start(psi);
    return process is not null ? new ProcessWrapper(process) : null;
  }

  public IProcess Start(string fileName)
  {
    var process = Process.Start(fileName);
    Guard.IsNotNull(process);
    return new ProcessWrapper(process);
  }

  public IProcess Start(string fileName, string arguments)
  {
    var process = Process.Start(fileName, arguments);
    Guard.IsNotNull(process);
    return new ProcessWrapper(process);
  }

  public IProcess? Start(ProcessStartInfo startInfo)
  {
    Guard.IsNotNull(startInfo);
    var process = Process.Start(startInfo);
    return process is not null ? new ProcessWrapper(process) : null;
  }

  public IProcess? Start(string fileName, string arguments, bool useShellExec)
  {
    var psi = new ProcessStartInfo()
    {
      FileName = fileName,
      Arguments = arguments,
      UseShellExecute = useShellExec
    };
    var process = Process.Start(psi);
    return process != null ? new ProcessWrapper(process) : null;
  }
  public async Task<int> StartAndWaitForExit(ProcessStartInfo startInfo, TimeSpan timeout)
  {
    using var process = Process.Start(startInfo);
    Guard.IsNotNull(process);

    using var cts = new CancellationTokenSource(timeout);
    await process.WaitForExitAsync(cts.Token);

    if (process.ExitCode != 0)
    {
      throw new ProcessStatusException(process.ExitCode);
    }

    return process.ExitCode;
  }

  public async Task<int> StartAndWaitForExit(string fileName, string arguments, bool useShellExec, TimeSpan timeout)
  {
    using var cts = new CancellationTokenSource(timeout);
    return await StartAndWaitForExit(fileName, arguments, useShellExec, cts.Token);
  }

  public async Task<int> StartAndWaitForExit(string fileName, string arguments, bool useShellExec, CancellationToken cancellationToken)
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
    return process.ExitCode;
  }
}
