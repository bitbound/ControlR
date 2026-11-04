using ControlR.DesktopClient.Common.Options;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Services;

public interface IDesktopPathProvider
{
  /// <summary>
  /// TODO: Temporary migration code for v0.31.x
  /// Returns the directory this client's settings lived in before the publisher became a directory in
  /// its path, which is where the files an older client wrote are found after the install moves.
  /// </summary>
  string GetLegacySettingsDirectory();

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
  public string GetLegacySettingsDirectory()
  {
    // Delete this member with the settings carry it reads, once no device is still running an agent
    // built before the publisher directory.
#if IS_WINDOWS
    return ControlR.DesktopClient.Windows.PathConstants.GetLegacySettingsDirectory(options.Value.InstanceId);
#elif IS_MACOS
    return ControlR.DesktopClient.Mac.PathConstants.GetLegacySettingsDirectory(options.Value.InstanceId);
#elif IS_LINUX
    return ControlR.DesktopClient.Linux.PathConstants.GetLegacySettingsDirectory(options.Value.InstanceId);
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
