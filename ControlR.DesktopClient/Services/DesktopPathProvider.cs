using ControlR.DesktopClient.Common.Options;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Services;

public interface IDesktopPathProvider
{
  /// <summary>
  /// TODO: Temporary migration code for v0.31.x
  /// Returns the path this client's settings file sat at before the publisher became a directory in it,
  /// which is where a settings file written by an older client is found after the install moves.
  /// </summary>
  string GetLegacySettingsPath();

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
  public string GetLegacySettingsPath()
  {
    // Delete this member with the settings move it reads, once no device is still running an agent
    // built before the publisher directory.
#if IS_WINDOWS
    return ControlR.DesktopClient.Windows.PathConstants.GetLegacySettingsPath(options.Value.InstanceId);
#elif IS_MACOS
    return ControlR.DesktopClient.Mac.PathConstants.GetLegacySettingsPath(options.Value.InstanceId);
#elif IS_LINUX
    return ControlR.DesktopClient.Linux.PathConstants.GetLegacySettingsPath(options.Value.InstanceId);
#else
    throw new PlatformNotSupportedException();
#endif
  }

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
