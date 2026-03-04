using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

internal interface IDisplayManagerWayland : IDisplayManager
{
  bool HasAnyCaptureSizes { get; }

  Task<IReadOnlyDictionary<string, PipeWireStream>> CreatePipeWireStreams(CancellationToken cancellationToken = default);
  bool TryGetCaptureSize(string deviceName, out Size size);
  void UpdateCaptureSize(string deviceName, int physicalWidth, int physicalHeight);
}

/// <summary>
/// Display manager for Wayland.
///
/// Note: Wayland does not provide a standardized way to query display information
/// without compositor-specific APIs
/// 
/// Terminology used by this manager:
/// - LogicalMonitorArea: portal/compositor-reported logical bounds (device-independent units).
/// - MonitorArea: physical pixel bounds (native pixels), used for capture and frame geometry.
/// - ScaleFactor: physical / logical (physical = logical * ScaleFactor).
///
/// Note on input coordinates:
/// Wayland RemoteDesktop absolute pointer APIs operate in compositor/logical coordinates,
/// so pointer conversion here uses LogicalMonitorArea rather than MonitorArea.
/// </summary>
internal class DisplayManagerWayland(
  TimeProvider timeProvider,
  IXdgDesktopPortal portalService,
  IPipeWireStreamFactory streamFactory,
  ILogger<DisplayManagerWayland> logger) : IDisplayManager, IDisplayManagerWayland, IDisposable
{
  private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(3);

  private readonly ConcurrentDictionary<string, Size> _captureSizes = new();
  private readonly SemaphoreSlim _displayLock = new(1, 1);
  private readonly TimeSpan _displayLockTimeout = TimeSpan.FromSeconds(5);
  private readonly Dictionary<string, uint> _displayNodeIds = [];
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerWayland> _logger = logger;
  private readonly IXdgDesktopPortal _portalService = portalService;
  private readonly IPipeWireStreamFactory? _streamFactory = streamFactory;
  private readonly TimeProvider _timeProvider = timeProvider;

  private bool _disposed;

  public bool HasAnyCaptureSizes => !_captureSizes.IsEmpty;

  public async Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY)
  {
    var findResult = await TryFindDisplay(displayName);
    if (!findResult.IsSuccess)
    {
      return Point.Empty;
    }

    var display = findResult.Value;
    // On Wayland, absolute pointer movement uses compositor/logical coordinates.
    // Convert normalized percentages into the selected display's logical coordinate space.
    var bounds = display.LogicalMonitorArea;

    var absoluteX = (int)(bounds.Left + bounds.Width * percentX);
    var absoluteY = (int)(bounds.Top + bounds.Height * percentY);

    return new Point(absoluteX, absoluteY);
  }

  public async Task<IReadOnlyDictionary<string, PipeWireStream>> CreatePipeWireStreams(CancellationToken cancellationToken = default)
  {
    var result = new Dictionary<string, PipeWireStream>();
    try
    {
      var connection = await _portalService.GetPipeWireConnection();
      if (connection is null || _streamFactory is null)
      {
        return result;
      }

      var portalStreams = await _portalService.GetScreenCastStreams();
      if (portalStreams is null || portalStreams.Count == 0)
      {
        return result;
      }

      foreach (var streamInfo in portalStreams.OrderBy(s => s.StreamIndex))
      {
        var deviceName = streamInfo.StreamIndex.ToString();

        int logicalWidth = 1920, logicalHeight = 1080;
        if (streamInfo.Properties.TryGetValue("size", out var sizeObj) && WaylandTupleParser.TryParseTuple2(sizeObj, out var sx, out var sy))
        {
          logicalWidth = sx;
          logicalHeight = sy;
        }

        try
        {
          var stream = _streamFactory.Create(streamInfo.NodeId, connection.Value.Fd, logicalWidth, logicalHeight);
          result[deviceName] = stream;
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to create PipeWire stream for {Device}", deviceName);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Error creating PipeWire streams.");
    }

    return result;
  }

  public void Dispose()
  {
    if (!_disposed)
    {
      _displayLock.Dispose();
      _disposed = true;
    }
  }

  public async Task<ImmutableList<DisplayInfo>> GetDisplays()
  {
    await EnsureDisplaysLoaded();
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    return _displays
      .Values
      .ToImmutableList();
  }

  public async Task<DisplayInfo?> GetPrimaryDisplay()
  {
    await EnsureDisplaysLoaded();
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    return _displays.Values.FirstOrDefault(x => x.IsPrimary)
      ?? _displays.Values.FirstOrDefault();
  }

  public async Task<Rectangle> GetVirtualScreenBounds()
  {
    try
    {
      await EnsureDisplaysLoaded();
      using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
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
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    await ReloadDisplaysImpl();
  }

  public Task<Result> SetPrivacyScreen(bool isEnabled)
  {
    throw new PlatformNotSupportedException("Privacy screen is only supported on Windows.");
  }

  public async Task<Result<DisplayInfo>> TryFindDisplay(string deviceName)
  {
    await EnsureDisplaysLoaded();
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    if (_displays.TryGetValue(deviceName, out var display))
    {
      return Result.Ok(display);
    }

    return Result.Fail<DisplayInfo>("Display not found.");
  }

  public bool TryGetCaptureSize(string deviceName, out Size size)
  {
    return _captureSizes.TryGetValue(deviceName, out size);
  }

  /// <summary>
  /// Gets the PipeWire NodeId for a given display device name.
  /// This is useful for mapping displays to their corresponding PipeWire streams.
  /// </summary>
  public async Task<Result<uint>> TryGetNodeId(string deviceName)
  {
    await EnsureDisplaysLoaded();
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    if (_displayNodeIds.TryGetValue(deviceName, out var nodeId))
    {
      return Result.Ok(nodeId);
    }

    return Result.Fail<uint>("Display not found.");
  }

  public void UpdateCaptureSize(string deviceName, int physicalWidth, int physicalHeight)
  {
    if (string.IsNullOrWhiteSpace(deviceName) || physicalWidth <= 0 || physicalHeight <= 0)
    {
      return;
    }

    var newSize = new Size(physicalWidth, physicalHeight);
    _captureSizes.AddOrUpdate(deviceName, newSize, (_, __) => newSize);
  }

  private async Task EnsureDisplaysLoaded()
  {
    if (_displays.IsEmpty)
    {
      using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
      if (_displays.IsEmpty)
      {
        await ReloadDisplaysImpl();
      }
    }
  }

  private async Task<Size> ProbePhysicalSize(uint nodeId, int logicalWidth, int logicalHeight)
  {
    if (_streamFactory == null || nodeId == 0)
    {
      return Size.Empty;
    }

    try
    {
      var connection = await _portalService.GetPipeWireConnection();
      if (connection is null)
      {
        return Size.Empty;
      }

      using var stream = _streamFactory.Create(nodeId, connection.Value.Fd, logicalWidth, logicalHeight);
      
      // Allow time for stream negotiation and first frame delivery.
      using var cts = new CancellationTokenSource(_probeTimeout);
      if (!await stream.WaitForFirstFrame(cts.Token))
      {
        return Size.Empty;
      }

      if (stream.Width > 0 && stream.Height > 0)
      {
        return new Size(stream.Width, stream.Height);
      }
    }
    catch (OperationCanceledException)
    {
      return Size.Empty;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to probe physical size for node {NodeId}", nodeId);
    }

    return Size.Empty;
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
        var orderedStreams = streams
          .OrderBy(x => x.StreamIndex)
          .ToList();

        // If the portal doesn't provide positions, fall back to horizontal layout.
        var fallbackOffsetX = 0;

        // Prefer portal-reported position (typically compositor coordinate space).
        var hasAnyPositions = orderedStreams.Any(s => s.Properties.TryGetValue("position", out var posObj)
          && WaylandTupleParser.TryParseTuple2(posObj, out _, out _));

        foreach (var stream in orderedStreams)
        {
          int logicalWidth = 1920, logicalHeight = 1080;

          int logicalLeft = 0;
          int logicalTop = 0;

          // Get logical dimensions from portal properties
          if (stream.Properties.TryGetValue("size", out var sizeObj) && WaylandTupleParser.TryParseTuple2(sizeObj, out var sizeX, out var sizeY))
          {
            logicalWidth = sizeX;
            logicalHeight = sizeY;
          }

          if (stream.Properties.TryGetValue("position", out var positionObj) && WaylandTupleParser.TryParseTuple2(positionObj, out var positionX, out var positionY))
          {
            logicalLeft = positionX;
            logicalTop = positionY;
          }

          var deviceName = stream.StreamIndex.ToString();

          // physicalWidth/Height are the dimensions of the capture in physical pixels (if known)
          var physicalWidth = logicalWidth;
          var physicalHeight = logicalHeight;

          if (_captureSizes.TryGetValue(deviceName, out var captureSize) && captureSize.Width > 0 && captureSize.Height > 0)
          {
            physicalWidth = captureSize.Width;
            physicalHeight = captureSize.Height;
          }
          else
          {
            // If we don't have the capture size yet, try to probe it briefly.
            // This ensures we get the correct physical resolution relative to the logical resolution
            // so that coordinate mapping (ScaleFactor) is correct.
            var probedSize = await ProbePhysicalSize(stream.NodeId, logicalWidth, logicalHeight);
            if (probedSize.Width > 0 && probedSize.Height > 0)
            {
              physicalWidth = probedSize.Width;
              physicalHeight = probedSize.Height;
              UpdateCaptureSize(deviceName, physicalWidth, physicalHeight);
            }
          }

          var scaleFactor = logicalWidth > 0
            ? (double)physicalWidth / logicalWidth
            : 1.0;

          var captureLeft = 0;
          var captureTop = 0;
          if (hasAnyPositions)
          {
            captureLeft = (int)Math.Round(logicalLeft * scaleFactor);
            captureTop = (int)Math.Round(logicalTop * scaleFactor);
          }
          else
          {
            captureLeft = fallbackOffsetX;
            captureTop = 0;
          }

          var display = new DisplayInfo
          {
            DeviceName = deviceName,
            DisplayName = $"Display {stream.StreamIndex + 1}",
            Index = stream.StreamIndex,
            LogicalMonitorArea = new Rectangle(logicalLeft, logicalTop, logicalWidth, logicalHeight),
            MonitorArea = new Rectangle(captureLeft, captureTop, physicalWidth, physicalHeight),
            WorkArea = new Rectangle(captureLeft, captureTop, physicalWidth, physicalHeight),
            IsPrimary = stream.StreamIndex == orderedStreams.Min(x => x.StreamIndex),
            ScaleFactor = scaleFactor
          };

          _displays[display.DeviceName] = display;

          // _displayNodeIds already populated by EnsureStreamsInitialized or set below
          if (!_displayNodeIds.ContainsKey(deviceName) && stream.NodeId != 0)
          {
            _displayNodeIds[deviceName] = stream.NodeId;
          }

          // Fallback layout: position displays horizontally side-by-side.
          if (!hasAnyPositions)
          {
            fallbackOffsetX += physicalWidth;
          }
        }

        _logger.LogInformation("Loaded {Count} Wayland display(s) from portal", streams.Count);
      }
      else
      {
        var defaultDisplay = new DisplayInfo
        {
          DeviceName = "0",
          DisplayName = "Display 1",
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
