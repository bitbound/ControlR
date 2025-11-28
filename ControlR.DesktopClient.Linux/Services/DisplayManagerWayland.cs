using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
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
internal class DisplayManagerWayland(
  ILogger<DisplayManagerWayland> logger,
  IWaylandPortalAccessor portalService) : IDisplayManager
{
  private readonly Lock _displayLock = new();
  private readonly Dictionary<string, uint> _displayNodeIds = new(); // Maps DeviceName to NodeId
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerWayland> _logger = logger;
  private readonly IWaylandPortalAccessor _portalService = portalService;

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
  /// <summary>
  /// Gets the PipeWire NodeId for a given display device name.
  /// This is useful for mapping displays to their corresponding PipeWire streams.
  /// </summary>
  public bool TryGetNodeId(string deviceName, out uint nodeId)
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();
      return _displayNodeIds.TryGetValue(deviceName, out nodeId);
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
      _displayNodeIds.Clear();

      var streams = _portalService.GetScreenCastStreams().GetAwaiter().GetResult();

      if (streams.Count > 0)
      {
        int offsetX = 0;

        for (int i = 0; i < streams.Count; i++)
        {
          var stream = streams[i];
          int logicalWidth = 1920, logicalHeight = 1080;

          // Get logical dimensions from portal properties
          if (stream.Properties.TryGetValue("size", out var sizeObj) && sizeObj is ValueTuple<int, int> sizeTuple)
          {
            logicalWidth = sizeTuple.Item1;
            logicalHeight = sizeTuple.Item2;
          }

          // For Wayland with ScreenCast, the logical dimensions are what we use
          // The actual physical dimensions will be determined by the ScreenGrabber when it creates the stream
          // We use logical dimensions here to match what the compositor reports
          var deviceName = i.ToString();

          var display = new DisplayInfo
          {
            DeviceName = deviceName,
            DisplayName = $"Wayland Display {i + 1}",
            MonitorArea = new Rectangle(offsetX, 0, logicalWidth, logicalHeight),
            WorkArea = new Rectangle(offsetX, 0, logicalWidth, logicalHeight),
            IsPrimary = i == 0,
            ScaleFactor = 1.0 // Will be updated by ScreenGrabber when stream is created
          };

          _displays[display.DeviceName] = display;
          _displayNodeIds[deviceName] = stream.NodeId;

          // Position displays horizontally side-by-side for multi-monitor setups
          offsetX += logicalWidth;
        }

        _logger.LogInformation("Loaded {Count} Wayland display(s) from portal", streams.Count);
      }
      else
      {
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
        _logger.LogWarning("Using fallback display info - portal returned no streams");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading Wayland display list");
    }
  }
}
