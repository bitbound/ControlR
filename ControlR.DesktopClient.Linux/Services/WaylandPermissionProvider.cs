using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.DevicesCommon.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Linux.Services;

public interface IWaylandPermissionProvider
{
  Task<bool> IsRemoteControlPermissionGranted();
  Task<bool> RequestRemoteControlPermission(bool force = false);
}

internal class WaylandPermissionProvider(
  IFileSystem fileSystem,
  IXdgDesktopPortalFactory xdgFactory,
  IOptionsMonitor<DesktopClientOptions> options,
  ILogger<WaylandPermissionProvider> logger) : IWaylandPermissionProvider
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<WaylandPermissionProvider> _logger = logger;
  private readonly IOptionsMonitor<DesktopClientOptions> _options = options;
  private readonly IXdgDesktopPortalFactory _xdgFactory = xdgFactory;

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
  public async Task<bool> RequestRemoteControlPermission(bool force = false)
  {
    try
    {
      using var xdgPortal = _xdgFactory.CreateNew();
      await xdgPortal.Initialize();
      _logger.LogInformation("RemoteDesktop permission granted via XDG portal");
      return await IsRemoteControlPermissionGranted();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error requesting RemoteDesktop permission");
      return false;
    }
  }
}
