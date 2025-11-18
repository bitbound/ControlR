using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

internal class WaylandInterop(ILogger<WaylandInterop> logger) : IWaylandInterop
{
  private readonly ILogger<WaylandInterop> _logger = logger;

  public bool IsScreenCastPermissionGranted()
  {
    return IsScreenCastPermissionGrantedAsync().GetAwaiter().GetResult();
  }

  public bool IsRemoteDesktopPermissionGranted()
  {
    return IsRemoteDesktopPermissionGrantedAsync().GetAwaiter().GetResult();
  }

  public void OpenWaylandPermissionsInfo()
  {
    try
    {
      var process = new System.Diagnostics.Process
      {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
          FileName = "xdg-open",
          Arguments = "https://github.com/flatpak/xdg-desktop-portal",
          UseShellExecute = false,
          CreateNoWindow = true
        }
      };
      process.Start();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error opening Wayland permissions info");
    }
  }

  private async Task<bool> IsScreenCastPermissionGrantedAsync()
  {
    try
    {
      using var portal = await XdgDesktopPortal.CreateAsync(_logger);
      return await portal.IsScreenCastAvailableAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking ScreenCast permission");
      return false;
    }
  }

  private async Task<bool> IsRemoteDesktopPermissionGrantedAsync()
  {
    try
    {
      using var portal = await XdgDesktopPortal.CreateAsync(_logger);
      return await portal.IsRemoteDesktopAvailableAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking RemoteDesktop permission");
      return false;
    }
  }
}
