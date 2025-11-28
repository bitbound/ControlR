using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.DevicesCommon.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Linux.Services;

public interface IWaylandPermissionProvider
{
  Task<bool> IsRemoteControlPermissionGranted();
  Task<bool> RequestRemoteControlPermission();
}

internal class WaylandPermissionProvider(
  IFileSystem fileSystem,
  IWaylandPortalAccessor portalAccessor,
  IOptionsMonitor<DesktopClientOptions> options,
  ILogger<WaylandPermissionProvider> logger) : IWaylandPermissionProvider
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<WaylandPermissionProvider> _logger = logger;
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;
  private readonly IWaylandPortalAccessor _portalAccessor = portalAccessor;

  public async Task<bool> IsRemoteControlPermissionGranted()
  {
    try
    {
      var tokenPath = PathConstants.GetWaylandRemoteDesktopRestoreTokenPath(_options.CurrentValue.InstanceId);
      return _fileSystem.FileExists(tokenPath);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking RemoteDesktop permission");
      return false;
    }
  }
  public async Task<bool> RequestRemoteControlPermission()
  {
    try
    {
      await _portalAccessor.Initialize();
      _logger.LogInformation("RemoteDesktop permission granted via XDG portal");
      return await IsRemoteControlPermissionGranted();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error requesting RemoteDesktop permission");
      return false;
    }
    finally
    {
      await _portalAccessor.Deinitialize();
    }
  }
}
