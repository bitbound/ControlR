using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.Shared.Primitives;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning disable CA1401 // P/Invokes should not be visible

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

[SupportedOSPlatform("macos")]
public static class UnixSocketClientInfoMac
{
  private const int LOCAL_PEERPID = 0x002;
  private const int SOL_LOCAL = 0;

  public static Result<ClientCredentials> GetClientCredentials(SafeHandle socketHandle)
  {
    try
    {
      if (socketHandle.IsInvalid || socketHandle.IsClosed)
      {
        return Result.Fail<ClientCredentials>("Socket handle is invalid or closed.");
      }

      // Try LOCAL_PEERPID first (more direct way to get PID on macOS)
      var pid = 0;
      var pidSize = sizeof(int);

      var result = getsockopt(
        (int)socketHandle.DangerousGetHandle(),
        SOL_LOCAL,
        LOCAL_PEERPID,
        ref pid,
        ref pidSize);

      if (result != 0 || pid == 0)
      {
        var error = Marshal.GetLastPInvokeError();
        return Result.Fail<ClientCredentials>($"Failed to get peer process ID using LOCAL_PEERPID. Error: {error}");
      }

      var executablePath = GetProcessExecutablePath(pid);
      if (string.IsNullOrWhiteSpace(executablePath))
      {
        return Result.Fail<ClientCredentials>($"Failed to get executable path for process {pid}.");
      }

      return Result.Ok(new ClientCredentials(pid, executablePath));
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
      // Try using proc_pidpath first (more reliable on macOS)
      var buffer = new byte[4096];
      var length = proc_pidpath(processId, buffer, (uint)buffer.Length);
      
      if (length > 0)
      {
        return System.Text.Encoding.UTF8.GetString(buffer, 0, length);
      }

      return string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }

  [DllImport("libc", EntryPoint = "getsockopt", SetLastError = true)]
  private static extern int getsockopt(
    int sockfd,
    int level,
    int optname,
    ref int optval,
    ref int optlen);

  [DllImport("libproc", EntryPoint = "proc_pidpath", SetLastError = true)]
  private static extern int proc_pidpath(
    int pid,
    [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
    uint buffersize);
}
