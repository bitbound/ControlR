using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.Shared.Primitives;
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
  IXdgDesktopPortal portalService) : IDisplayManager
{
  private readonly Lock _displayLock = new();
  private readonly Dictionary<string, uint> _displayNodeIds = new(); // Maps DeviceName to NodeId
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerWayland> _logger = logger;
  private readonly IXdgDesktopPortal _portalService = portalService;

  public async Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY)
  {
    var findResult = await TryFindDisplay(displayName);
    if (!findResult.IsSuccess)
    {
      return Point.Empty;
    }
    
    var display = findResult.Value;
    var bounds = display.MonitorArea;
    var absoluteX = (int)(bounds.Width * percentX);
    var absoluteY = (int)(bounds.Height * percentY);

    return new Point(absoluteX, absoluteY);
  }
  public async Task<ImmutableList<DisplayInfo>> GetDisplays()
  {
    await EnsureDisplaysLoaded();
    using var locker = _displayLock.EnterScope();
    var displayDtos = _displays
      .Values
      .ToImmutableList();
    return displayDtos;
  }
  public async Task<DisplayInfo?> GetPrimaryDisplay()
  {
    await EnsureDisplaysLoaded();
    using var locker = _displayLock.EnterScope();
    return _displays.Values.FirstOrDefault(x => x.IsPrimary)
      ?? _displays.Values.FirstOrDefault();
  }
  public async Task<Rectangle> GetVirtualScreenBounds()
  {
    try
    {
      await EnsureDisplaysLoaded();
      using var locker = _displayLock.EnterScope();
      if (_displays.IsEmpty)
      {
        return Rectangle.Empty;
      }

      var minX = _displays.Values.Min(d => d.MonitorArea.Left);
      var minY = _displays.Values.Min(d => d.MonitorArea.Top);
      var maxX = _displays.Values.Max(d => d.MonitorArea.Right);
      var maxY = _displays.Values.Max(d => d.MonitorArea.Bottom);

      return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting virtual screen bounds on Wayland");
      return new Rectangle(0, 0, 1920, 1080);
    }
  }
  public async Task ReloadDisplays()
  {
    await ReloadDisplaysImpl();
  }

  public Task<Result> SetPrivacyScreen(bool isEnabled)
  {
    throw new PlatformNotSupportedException("Privacy screen is only supported on Windows.");
  }

  public async Task<Result<DisplayInfo>> TryFindDisplay(string deviceName)
  {
    await EnsureDisplaysLoaded();
    using var locker = _displayLock.EnterScope();
    if (_displays.TryGetValue(deviceName, out var display))
    {
      return Result.Ok(display);
    }

    return Result.Fail<DisplayInfo>("Display not found.");
  }
  /// <summary>
  /// Gets the PipeWire NodeId for a given display device name.
  /// This is useful for mapping displays to their corresponding PipeWire streams.
  /// </summary>
  public async Task<Result<uint>> TryGetNodeId(string deviceName)
  {
    await EnsureDisplaysLoaded();
    using var locker = _displayLock.EnterScope();
    if (_displayNodeIds.TryGetValue(deviceName, out var nodeId))
    {
      return Result.Ok(nodeId);
    }

    return Result.Fail<uint>("Display not found.");
  }

  private async Task EnsureDisplaysLoaded()
  {
    if (_displays.IsEmpty)
    {
      await ReloadDisplaysImpl();
    }
  }
  private async Task ReloadDisplaysImpl()
  {
    try
    {
      _displays.Clear();
      _displayNodeIds.Clear();

      var streams = await _portalService.GetScreenCastStreams();

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
            DisplayName = $"Display {i + 1}",
            Index = i,
            MonitorArea = new Rectangle(offsetX, 0, logicalWidth, logicalHeight),
            WorkArea = new Rectangle(offsetX, 0, logicalWidth, logicalHeight),
            IsPrimary = i == 0,
            ScaleFactor = 1.0
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
          DisplayName = "Display 0",
          Index = 0,
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
