namespace ControlR.Libraries.Ipc;
public static class IpcPipeNames
{
  public static string GetPipeName()
  {
    if (OperatingSystem.IsWindows())
    {
      return GetWindowsPipeName();
    }

    if (OperatingSystem.IsMacOS())
    {
      return GetMacPipeName();
    }

    if (OperatingSystem.IsLinux())
    {
      return GetLinuxPipeName();
    }

    throw new PlatformNotSupportedException();
  }

  public static string GetWindowsPipeName()
  {
#if DEBUG
    return "controlr-ipc-server-debug";
#else
    return "controlr-ipc-server";
#endif
  }

  public static string GetMacPipeName()
  {
#if DEBUG
    return "/tmp/controlr-ipc-server-debug";
#else
    return "/tmp/controlr-ipc-server";
#endif
  }

  public static string GetLinuxPipeName()
  {
#if DEBUG
    return "/tmp/controlr-ipc-server-debug";
#else
    return "/tmp/controlr-ipc-server";
#endif
  }
}
