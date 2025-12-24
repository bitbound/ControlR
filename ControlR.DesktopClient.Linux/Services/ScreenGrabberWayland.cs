using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ControlR.DesktopClient.Linux.Services;

/// <summary>
/// Screen grabber for Wayland using XDG Desktop Portal and PipeWire.
/// <para>
/// This implementation uses the XDG Desktop Portal for permission management and stream setup,
/// PipeWire for receiving video frames, and converts frames to SKBitmap for compatibility.
/// </para>
/// <para>
/// Wayland security model workflow:
/// <list type="number">
/// <item>Create ScreenCast session via XDG Desktop Portal</item>
/// <item>User grants permission via system dialog (one-time)</item>
/// <item>Portal provides PipeWire node IDs for screen content</item>
/// <item>PipeWire streams deliver frames via callbacks</item>
/// <item>Frames are converted to SKBitmap for capture operations</item>
/// </list>
/// </para>
/// <para>
/// Supports both single and multi-display configurations. Multi-display captures are composited
/// into a single bitmap representing the virtual screen bounds.
/// </para>
/// </summary>
internal class ScreenGrabberWayland(
  IDisplayManager displayManager,
  IXdgDesktopPortal portalService,
  IPipeWireStreamFactory streamFactory,
  ILogger<ScreenGrabberWayland> logger) : IScreenGrabber
{
  private const int StreamStartPollingIntervalMs = 100;
  private const int StreamStartTimeoutMs = 3_000;

  private readonly IDisplayManager _displayManager = displayManager;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private readonly ILogger<ScreenGrabberWayland> _logger = logger;
  private readonly IXdgDesktopPortal _portalService = portalService;
  private readonly IPipeWireStreamFactory _streamFactory = streamFactory;
  private readonly ConcurrentDictionary<string, PipeWireStream> _streams = [];

  private bool _disposed;
  private bool _isInitialized;

  /// <summary>
  /// Gets the PipeWire streams mapped by display device name
  /// </summary>
  public IReadOnlyDictionary<string, PipeWireStream> Streams => _streams;

  public async Task<CaptureResult> CaptureAllDisplays(bool captureCursor = true)
  {
    try
    {
      if (!_isInitialized)
      {
        return CaptureResult.Fail("Wayland screen capture not initialized. Call InitializeAsync first.");
      }

      if (_streams.IsEmpty)
      {
        return CaptureResult.Fail("No streams available.");
      }

      // For single display, just return that display's capture
      if (_streams.Count == 1)
      {
        var stream = _streams.Values.First();
        return CaptureStream(stream);
      }

      // For multiple displays, we need to composite them together
      // This requires knowing the virtual screen bounds and each display's position
      var virtualBounds = await _displayManager.GetVirtualScreenBounds();
      var displays = await _displayManager.GetDisplays();

      var compositeBitmap = new SKBitmap(virtualBounds.Width, virtualBounds.Height);
      using var canvas = new SKCanvas(compositeBitmap);
      canvas.Clear(SKColors.Black);

      foreach (var display in displays)
      {
        if (!_streams.TryGetValue(display.DeviceName, out var stream))
        {
          _logger.LogWarning("No stream found for display {DisplayName}", display.DeviceName);
          continue;
        }

        var captureResult = CaptureStream(stream);
        if (!captureResult.IsSuccess || captureResult.Bitmap is null)
        {
          _logger.LogWarning("Failed to capture display {DisplayName}", display.DeviceName);
          continue;
        }

        using var displayBitmap = captureResult.Bitmap;
        var destRect = SKRect.Create(
          display.MonitorArea.X - virtualBounds.X,
          display.MonitorArea.Y - virtualBounds.Y,
          display.MonitorArea.Width,
          display.MonitorArea.Height);

        canvas.DrawBitmap(displayBitmap, destRect);
      }

      return CaptureResult.Ok(compositeBitmap, isUsingGpu: false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing all displays on Wayland");
      return CaptureResult.Fail(ex);
    }
  }
  public async Task<CaptureResult> CaptureDisplay(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool forceKeyFrame = false)
  {
    try
    {
      if (!_isInitialized)
      {
        return CaptureResult.Fail("Wayland screen capture not initialized. Call InitializeAsync first.");
      }

      // Find the stream for the target display
      if (!_streams.TryGetValue(targetDisplay.DeviceName, out var stream))
      {
        _logger.LogWarning("No stream found for display {DisplayName}. Falling back to first available stream.", targetDisplay.DeviceName);
        stream = _streams.Values.FirstOrDefault();
        if (stream is null)
        {
          return CaptureResult.Fail($"No stream available for display {targetDisplay.DeviceName}");
        }
      }

      return CaptureStream(stream);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing display {DisplayName} on Wayland", targetDisplay.DeviceName);
      return CaptureResult.Fail(ex);
    }
  }
  public async ValueTask DisposeAsync()
  {
    if (_disposed)
    {
      return;
    }

    Disposer.DisposeAll(_streams.Values);
    _streams.Clear();
    _initLock?.Dispose();
    _disposed = true;
  }
  public async Task Initialize(CancellationToken cancellationToken)
  {
    if (_isInitialized)
    {
      return;
    }

    await _initLock.WaitAsync(cancellationToken);
    try
    {
      if (_isInitialized)
      {
        return;
      }

      // Get portal connection (reuses existing session if already initialized)
      var portalStreams = await _portalService.GetScreenCastStreams();
      var connection = await _portalService.GetPipeWireConnection();

      if (portalStreams.Count == 0 || connection is null)
      {
        throw new InvalidOperationException("Failed to get portal streams or connection");
      }

      _logger.LogInformation("Using portal session with {Count} stream(s)", portalStreams.Count);

      // Create a PipeWireStream for each display
      // Each stream corresponds to a monitor when using ScreenCast + Remote Desktop
      for (int i = 0; i < portalStreams.Count; i++)
      {
        var streamInfo = portalStreams[i];
        int logicalWidth = 0, logicalHeight = 0;

        if (streamInfo.Properties.TryGetValue("size", out var sizeObj) && sizeObj is ValueTuple<int, int> sizeTuple)
        {
          logicalWidth = sizeTuple.Item1;
          logicalHeight = sizeTuple.Item2;
          _logger.LogInformation("Portal stream {Index} reported logical dimensions: {Width}x{Height}", i, logicalWidth, logicalHeight);
        }

        // Pass logical dimensions so PipeWireStream can calculate scale factor
        var stream = _streamFactory.Create(streamInfo.NodeId, connection.Value.Fd, logicalWidth, logicalHeight);

        // Map this stream to the display device name (which matches the index)
        var deviceName = streamInfo.StreamIndex.ToString();
        _streams[deviceName] = stream;

        // Wait for the stream to start and have frames available
        for (var j = 0; j < StreamStartTimeoutMs / StreamStartPollingIntervalMs; j++)
        {
          if (stream.IsStreaming && stream.TryGetLatestFrame(out _))
          {
            break;
          }
          await Task.Delay(StreamStartPollingIntervalMs, cancellationToken);
        }

        // Log actual physical dimensions
        if (stream.ActualWidth > 0 && stream.ActualHeight > 0)
        {
          _logger.LogInformation(
            "Stream {Index} initialized with physical dimensions: {Width}x{Height} (scale: {Scale:F2}x)",
            i, stream.ActualWidth, stream.ActualHeight, stream.ScaleFactor);
        }
      }

      _isInitialized = true;
      _logger.LogInformation("Wayland screen capture fully initialized");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error initializing Wayland screen capture");
      throw;
    }
    finally
    {
      _initLock.Release();
    }
  }

  private CaptureResult CaptureStream(PipeWireStream stream)
  {
    try
    {
      var frameData = stream.GetLatestFrame();
      if (frameData is null)
      {
        return CaptureResult.Fail("No frame available yet. Stream may still be initializing.");
      }

      var info = new SKImageInfo(frameData.Width, frameData.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
      var bitmap = new SKBitmap();

      var gcHandle = GCHandle.Alloc(frameData.Data, GCHandleType.Pinned);
      try
      {
        var dataPtr = gcHandle.AddrOfPinnedObject();
        var contextPtr = GCHandle.ToIntPtr(gcHandle);

        // InstallPixels takes ownership of the pixel data pointer
        // We pass a release delegate to unpin the GCHandle when the bitmap is disposed
        if (!bitmap.InstallPixels(info, dataPtr, frameData.Stride, (address, context) =>
        {
          if (context is IntPtr ptr && ptr != IntPtr.Zero)
          {
            GCHandle.FromIntPtr(ptr).Free();
          }
        }, contextPtr))
        {
          gcHandle.Free();
          return CaptureResult.Fail("Failed to install pixels into SKBitmap");
        }
      }
      catch
      {
        gcHandle.Free();
        throw;
      }

      return CaptureResult.Ok(bitmap, isUsingGpu: false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing stream");
      return CaptureResult.Fail(ex);
    }
  }
}
