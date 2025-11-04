using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Win32.SafeHandles;

namespace ControlR.Libraries.NativeInterop.Windows;

[SupportedOSPlatform("windows")]
public static class PipeClientInfo
{
  public static Result<ClientCredentials> GetClientCredentials(SafeHandle pipeHandle)
  {
    try
    {
      if (pipeHandle.IsInvalid || pipeHandle.IsClosed)
      {
        return Result.Fail<ClientCredentials>("Pipe handle is invalid or closed.");
      }

      if (!GetNamedPipeClientProcessId(pipeHandle, out var processId))
      {
        var error = Marshal.GetLastWin32Error();
        return Result.Fail<ClientCredentials>($"Failed to get client process ID. Win32 error: {error}");
      }

      if (processId == 0)
      {
        return Result.Fail<ClientCredentials>("Client process ID is 0.");
      }

      var executablePath = GetProcessExecutablePath(processId);
      if (string.IsNullOrWhiteSpace(executablePath))
      {
        return Result.Fail<ClientCredentials>($"Failed to get executable path for process {processId}.");
      }

      return Result.Ok(new ClientCredentials(processId, executablePath));
    }
    catch (Exception ex)
    {
      return Result.Fail<ClientCredentials>($"Error getting client credentials: {ex.Message}");
    }
  }

  private static string GetProcessExecutablePath(int processId)
  {
    try
    {
      using var process = Process.GetProcessById(processId);
      return process.MainModule?.FileName ?? string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetNamedPipeClientProcessId(
    SafeHandle pipe,
    out int clientProcessId);
}

public record ClientCredentials(int ProcessId, string ExecutablePath);
