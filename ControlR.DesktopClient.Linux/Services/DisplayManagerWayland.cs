using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

/// <summary>
/// Display manager for Wayland.
///
/// Note: Wayland does not provide a standardized way to query display information
/// without compositor-specific APIs. We can attempt to use environment variables
/// or fall back to reasonable defaults.
///
/// Some compositors expose display info via wlr-output-management protocol,
/// but this is not universal.
/// </summary>
internal class DisplayManagerWayland(ILogger<DisplayManagerWayland> logger) : IDisplayManager
{
  private readonly Lock _displayLock = new();
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerWayland> _logger = logger;

  public Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY)
  {
    if (!TryFindDisplay(displayName, out var display))
    {
      return Task.FromResult(Point.Empty);
    }

    var bounds = display.MonitorArea;
    var absoluteX = (int)(bounds.Left + bounds.Width * percentX);
    var absoluteY = (int)(bounds.Top + bounds.Height * percentY);

    return Task.FromResult(new Point(absoluteX, absoluteY));
  }

  public Task<ImmutableList<DisplayInfo>> GetDisplays()
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();

      var displayDtos = _displays
        .Values
        .ToImmutableList();

      return Task.FromResult(displayDtos);
    }
  }

  public DisplayInfo? GetPrimaryDisplay()
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();
      return _displays.Values.FirstOrDefault(x => x.IsPrimary)
        ?? _displays.Values.FirstOrDefault();
    }
  }

  public Rectangle GetVirtualScreenBounds()
  {
    try
    {
      lock (_displayLock)
      {
        EnsureDisplaysLoaded();
        if (_displays.Count == 0)
        {
          return Rectangle.Empty;
        }

        var minX = _displays.Values.Min(d => d.MonitorArea.Left);
        var minY = _displays.Values.Min(d => d.MonitorArea.Top);
        var maxX = _displays.Values.Max(d => d.MonitorArea.Right);
        var maxY = _displays.Values.Max(d => d.MonitorArea.Bottom);

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting virtual screen bounds on Wayland");
      return new Rectangle(0, 0, 1920, 1080);
    }
  }

  public Task ReloadDisplays()
  {
    lock (_displayLock)
    {
      ReloadDisplaysImpl();
    }
    return Task.CompletedTask;
  }

  public bool TryFindDisplay(string deviceName, [NotNullWhen(true)] out DisplayInfo? display)
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();
      return _displays.TryGetValue(deviceName, out display);
    }
  }

  private void EnsureDisplaysLoaded()
  {
    if (_displays.Count == 0)
    {
      ReloadDisplaysImpl();
    }
  }

  private void ReloadDisplaysImpl()
  {
    try
    {
      _displays.Clear();

      // Wayland doesn't provide a standardized way to query displays
      // We'll create a default display entry
      // In a full implementation, this would use compositor-specific protocols
      // or wlr-output-management if available

      var defaultDisplay = new DisplayInfo
      {
        DeviceName = "0",
        DisplayName = "Wayland Display",
        MonitorArea = new Rectangle(0, 0, 1920, 1080),
        WorkArea = new Rectangle(0, 0, 1920, 1080),
        IsPrimary = true,
        ScaleFactor = 1.0
      };

      _displays[defaultDisplay.DeviceName] = defaultDisplay;

      _logger.LogInformation(
        "Loaded default Wayland display. " +
        "Actual display information requires compositor-specific implementation.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading Wayland display list");
    }
  }
}
