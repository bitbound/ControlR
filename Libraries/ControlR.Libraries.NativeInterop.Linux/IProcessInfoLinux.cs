using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ControlR.Libraries.NativeInterop.Linux;

[SupportedOSPlatform("linux")]
public interface IProcessInfoLinux
{
  /// <summary>
  /// Returns the pid of the process that is currently the parent of the calling process.
  /// </summary>
  int GetParentProcessId();
}

[SupportedOSPlatform("linux")]
public sealed class ProcessInfoLinux : IProcessInfoLinux
{
  public int GetParentProcessId()
  {
    return GetParentProcessIdNative();
  }

  [DllImport("libc", EntryPoint = "getppid")]
  private static extern int GetParentProcessIdNative();
}
