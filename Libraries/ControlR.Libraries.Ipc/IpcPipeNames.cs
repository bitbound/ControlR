using ControlR.Libraries.Branding;

namespace ControlR.Libraries.Ipc;
public static class IpcPipeNames
{
  public static string GetPipeName(string? instanceId)
  {
    if (OperatingSystem.IsWindows())
    {
      return GetWindowsPipeName(instanceId);
    }

    if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
    {
      return GetUnixPipeName(instanceId);
    }

    throw new PlatformNotSupportedException();
  }

  public static string GetUnixPipeName(string? instanceId)
  {
#if DEBUG
    var pipeName = $"/tmp/{BrandingConstants.IpcPipeBaseName}-debug";
#else
    var pipeName = $"/tmp/{BrandingConstants.IpcPipeBaseName}";
#endif
    if (string.IsNullOrWhiteSpace(instanceId))
    {
      return pipeName;
    }
    return $"{pipeName}-{instanceId.Replace(".", "-")}";
  }

  public static string GetWindowsPipeName(string? instanceId)
  {
#if DEBUG
    var pipeName = $"{BrandingConstants.IpcPipeBaseName}-debug";
#else
    var pipeName = $"{BrandingConstants.IpcPipeBaseName}";
#endif
    if (string.IsNullOrWhiteSpace(instanceId))
    {
      return pipeName;
    }
    return $"{pipeName}-{instanceId.Replace(".", "-")}";
  }
}
