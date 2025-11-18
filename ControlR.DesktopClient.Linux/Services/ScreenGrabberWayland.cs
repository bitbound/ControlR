using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Linux.Services;

/// <summary>
/// Screen grabber for Wayland using XDG Desktop Portal and PipeWire.
///
/// Implementation:
/// - Uses XDG Desktop Portal for permission management and stream setup
/// - Uses PipeWire for receiving video frames
/// - Converts frames to SKBitmap for compatibility with existing code
///
/// Wayland security model:
/// 1. Create ScreenCast session via portal
/// 2. User grants permission via system dialog (one-time)
/// 3. Portal provides PipeWire stream with screen content
/// 4. Application receives frames via PipeWire callbacks
/// </summary>
internal class ScreenGrabberWayland(
  IDisplayManager displayManager,
  ILogger<ScreenGrabberWayland> logger) : IScreenGrabber, IDisposable
{
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private readonly ILogger<ScreenGrabberWayland> _logger = logger;

  private bool _disposed;
  private bool _isInitialized;
  private XdgDesktopPortal? _portal;
  private string? _sessionHandle;
  private PipeWireStream? _stream;


  public CaptureResult CaptureAllDisplays(bool captureCursor = true)
  {
    try
    {
      if (!EnsureInitializedAsync().GetAwaiter().GetResult())
      {
        //_permissionDenied = true;
        return CaptureResult.Fail(
          "Failed to initialize Wayland screen capture. " +
          "XDG Desktop Portal ScreenCast may not be available, or permission was denied.");
      }

      var frameData = _stream?.GetLatestFrame();
      if (frameData is null)
      {
        return CaptureResult.Fail("No frame available yet. Stream may still be initializing.");
      }

      var info = new SKImageInfo(frameData.Width, frameData.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
      var bitmap = new SKBitmap(info);

      unsafe
      {
        var dstBase = (byte*)bitmap.GetPixels();
        var dstRowBytes = bitmap.Info.RowBytes; // width * 4 for BGRA8888
        var srcRowBytes = frameData.Stride > 0 ? frameData.Stride : dstRowBytes;

        fixed (byte* srcBase = frameData.Data)
        {
          var copyBytesPerRow = Math.Min(dstRowBytes, srcRowBytes);
          for (var y = 0; y < frameData.Height; y++)
          {
            var srcRow = srcBase + (y * srcRowBytes);
            var dstRow = dstBase + (y * dstRowBytes);
            Buffer.MemoryCopy(srcRow, dstRow, copyBytesPerRow, copyBytesPerRow);
          }
        }
      }

      return CaptureResult.Ok(bitmap, isUsingGpu: false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing all displays on Wayland");
      return CaptureResult.Fail(ex);
    }
  }

  public CaptureResult CaptureDisplay(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool forceKeyFrame = false)
  {
    // For Wayland, we capture all displays and return the composite
    // Individual display selection would require multiple portal sessions
    return CaptureAllDisplays(captureCursor);
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    _stream?.Dispose();
    _stream = null;

    _portal?.Dispose();
    _portal = null;

    _initLock?.Dispose();

    _disposed = true;
  }


  private async Task<bool> EnsureInitializedAsync()
  {
    if (_isInitialized)
    {
      return true;
    }

    await _initLock.WaitAsync();
    try
    {
      if (_isInitialized)
      {
        return true;
      }

      _portal = await XdgDesktopPortal.CreateAsync(_logger);

      if (!await _portal.IsScreenCastAvailableAsync())
      {
        _logger.LogError(
          "XDG Desktop Portal ScreenCast is not available. " +
          "Ensure xdg-desktop-portal and a backend (xdg-desktop-portal-gtk, -kde, -gnome, or -wlr) are installed.");
        return false;
      }

      // Create ScreenCast session
      var sessionResult = await _portal.CreateScreenCastSessionAsync();
      if (!sessionResult.IsSuccess || sessionResult.Value is null)
      {
        _logger.LogError("Failed to create ScreenCast session: {Error}", sessionResult.Reason);
        return false;
      }

      _sessionHandle = sessionResult.Value;
      _logger.LogInformation("Created ScreenCast session: {Session}", _sessionHandle);

      // Select sources (monitors)
      var selectResult = await _portal.SelectScreenCastSourcesAsync(
        _sessionHandle,
        sourceTypes: 1,  // Monitor
        multipleSources: true,
        cursorMode: 2);  // Embedded cursor

      if (!selectResult.IsSuccess)
      {
        _logger.LogError("Failed to select ScreenCast sources: {Error}", selectResult.Reason);
        return false;
      }

      // Start the session (shows permission dialog to user)
      var startResult = await _portal.StartScreenCastAsync(_sessionHandle);
      if (!startResult.IsSuccess || startResult.Value is null)
      {
        _logger.LogError("Failed to start ScreenCast: {Error}", startResult.Reason);
        return false;
      }

      var streams = startResult.Value;
      if (streams.Count == 0)
      {
        _logger.LogError("No streams returned from ScreenCast portal");
        return false;
      }

      _logger.LogInformation("ScreenCast started with {Count} stream(s)", streams.Count);

      // Open PipeWire remote
      var fdResult = await _portal.OpenPipeWireRemoteAsync(_sessionHandle);
      if (!fdResult.IsSuccess || fdResult.Value is null)
      {
        _logger.LogError("Failed to open PipeWire remote: {Error}", fdResult.Reason);
        return false;
      }

      // Create PipeWire stream for the first video stream
      var nodeId = streams[0].NodeId;
      _stream = new PipeWireStream(_logger, nodeId, fdResult.Value);

      // Wait briefly for the stream to start; poll for readiness
      var started = false;
      for (var i = 0; i < 10; i++)
      {
        if (_stream.IsStreaming)
        {
          started = true;
          break;
        }
        await Task.Delay(100);
      }
      if (!started)
      {
        _logger.LogWarning("PipeWire stream created but not yet streaming. Frames may not be available immediately.");
      }

      _isInitialized = true;
      _logger.LogInformation("Wayland screen capture fully initialized");
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error initializing Wayland screen capture");
      return false;
    }
    finally
    {
      _initLock.Release();
    }
  }
}
