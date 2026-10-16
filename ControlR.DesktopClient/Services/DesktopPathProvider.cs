using ControlR.DesktopClient.Common.Options;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Services;

public interface IDesktopPathProvider
{
  /// <summary>
  /// Returns the path to the desktop client's settings file.
  /// </summary>
  string GetSettingsPath();
}

/// <summary>
/// Paths the desktop client owns, resolved for the instance it is running as.
/// </summary>
public class DesktopPathProvider(IOptions<DesktopClientOptions> options) : IDesktopPathProvider
{
  public string GetSettingsPath()
  {
#if IS_WINDOWS
    return ControlR.DesktopClient.Windows.PathConstants.GetSettingsPath(options.Value.InstanceId);
#elif IS_MACOS
    return ControlR.DesktopClient.Mac.PathConstants.GetSettingsPath(options.Value.InstanceId);
#elif IS_LINUX
    return ControlR.DesktopClient.Linux.PathConstants.GetSettingsPath(options.Value.InstanceId);
#else
    throw new PlatformNotSupportedException();
#endif
  }
}
