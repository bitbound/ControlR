using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Primitives;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning disable CA1401 // P/Invokes should not be visible

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

[SupportedOSPlatform("linux")]
public static class UnixSocketClientInfoLinux
{
  private const int SOL_SOCKET = 1;
  private const int SO_PEERCRED = 17;

  public static Result<ClientCredentials> GetClientCredentials(SafeHandle socketHandle)
  {
    try
    {
      if (socketHandle.IsInvalid || socketHandle.IsClosed)
      {
        return Result.Fail<ClientCredentials>("Socket handle is invalid or closed.");
      }

      var ucred = new Ucred();
      var ucredSize = Marshal.SizeOf<Ucred>();

      var result = getsockopt(
        (int)socketHandle.DangerousGetHandle(),
        SOL_SOCKET,
        SO_PEERCRED,
        ref ucred,
        ref ucredSize);

      if (result != 0)
      {
        var error = Marshal.GetLastWin32Error();
        return Result.Fail<ClientCredentials>($"Failed to get peer credentials. Error: {error}");
      }

      if (ucred.pid == 0)
      {
        return Result.Fail<ClientCredentials>("Client process ID is 0.");
      }

      var executablePath = GetProcessExecutablePath(ucred.pid);
      if (string.IsNullOrWhiteSpace(executablePath))
      {
        return Result.Fail<ClientCredentials>($"Failed to get executable path for process {ucred.pid}.");
      }

      return Result.Ok(new ClientCredentials(ucred.pid, executablePath));
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
      var exePath = $"/proc/{processId}/exe";
      if (File.Exists(exePath))
      {
        // ReadLink to resolve the symlink
        var link = File.ResolveLinkTarget(exePath, returnFinalTarget: true);
        return link?.FullName ?? string.Empty;
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
    ref Ucred optval,
    ref int optlen);

  [StructLayout(LayoutKind.Sequential)]
  private struct Ucred
  {
    public int pid;
    public uint uid;
    public uint gid;
  }
}

public record ClientCredentials(int ProcessId, string ExecutablePath);
