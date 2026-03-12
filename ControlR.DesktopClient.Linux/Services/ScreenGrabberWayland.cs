using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Linux;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Collections.Concurrent;

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
  TimeProvider timeProvider,
  IDisplayManagerWayland displayManager,
  ILogger<ScreenGrabberWayland> logger) : IScreenGrabber
{
  private const string CaptureModePipeWire = "WaylandPipeWire";
  private const int StreamStartPollingIntervalMs = 100;
  private const int StreamStartTimeoutMs = 3_000;
  private static readonly TimeSpan _recoveryCooldown = TimeSpan.FromSeconds(2);
  private static readonly TimeSpan _streamStaleThreshold = TimeSpan.FromSeconds(2);

  private readonly IDisplayManagerWayland _displayManager = displayManager;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private readonly ILogger<ScreenGrabberWayland> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;

  private bool _disposed;
  private bool _isInitialized;
  private long _lastRecoveryAttemptUtcTicks;
  private ConcurrentDictionary<string, PipeWireStream> _streams = new();


  public async Task<CaptureResult> CaptureAllDisplays(bool captureCursor = true)
  {
    try
    {
      if (!_isInitialized)
      {
        return CaptureResult.Fail("Wayland screen capture not initialized. Call InitializeAsync first.");
      }

      if (_streams.Count == 0)
      {
        return CaptureResult.Fail("No streams available.");
      }

      // For single display, just return that display's capture
      if (_streams.Count == 1)
      {
        var kvp = _streams.First();
        var deviceName = kvp.Key;
        var stream = kvp.Value;
        var result = await CaptureStream(deviceName, stream, CancellationToken.None);
        if (result.IsSuccess && result.Bitmap is not null)
        {
          _displayManager.UpdateCaptureSize(deviceName, result.Bitmap.Width, result.Bitmap.Height);
        }
        return result;
      }

      // For multiple displays, we need to composite them together
      // This requires knowing the virtual screen bounds and each display's position
      var virtualBounds = await _displayManager.GetVirtualScreenLayoutBounds();
      if (virtualBounds.Width == 0 && virtualBounds.Height == 0)
      {
        _logger.LogError("Virtual screen bounds are invalid: {Bounds}", virtualBounds);
        return CaptureResult.Fail("Virtual screen bounds are invalid.");
      }

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

        using var captureResult = await CaptureStream(display.DeviceName, stream, CancellationToken.None);
        if (!captureResult.IsSuccess || captureResult.Bitmap is null)
        {
          _logger.LogWarning("Failed to capture display {DisplayName}", display.DeviceName);
          continue;
        }

        _displayManager.UpdateCaptureSize(display.DeviceName, captureResult.Bitmap.Width, captureResult.Bitmap.Height);

        var destRect = SKRect.Create(
          display.LayoutBounds.X - virtualBounds.X,
          display.LayoutBounds.Y - virtualBounds.Y,
          display.LayoutBounds.Width,
          display.LayoutBounds.Height);

        canvas.DrawBitmap(captureResult.Bitmap, destRect);
      }

      return CaptureResult.Ok(compositeBitmap, captureMode: CaptureModePipeWire);
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

      var result = await CaptureStream(targetDisplay.DeviceName, stream, CancellationToken.None);
      if (result.IsSuccess && result.Bitmap is not null)
      {
        _displayManager.UpdateCaptureSize(targetDisplay.DeviceName, result.Bitmap.Width, result.Bitmap.Height);
      }
      return result;
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

    DisposeStreams();
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

      var streams = await _displayManager.CreatePipeWireStreams(cancellationToken: cancellationToken);
      if (streams.Count == 0)
      {
        throw new InvalidOperationException("Failed to get portal streams or connection");
      }

      _logger.LogInformation("Created {Count} PipeWire stream(s)", streams.Count);

      await WaitForStreamsReady(streams, cancellationToken);
      ReplaceStreams(streams);

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

  internal static bool ShouldRecoverStream(
    DateTime nowUtc,
    DateTime createdUtc,
    DateTime? lastFrameReceivedUtc,
    TimeSpan staleThreshold,
    DateTime? lastRecoveryAttemptUtc,
    TimeSpan recoveryCooldown)
  {
    var mostRecentActivityUtc = lastFrameReceivedUtc ?? createdUtc;
    if (nowUtc - mostRecentActivityUtc < staleThreshold)
    {
      return false;
    }

    if (lastRecoveryAttemptUtc is not null && nowUtc - lastRecoveryAttemptUtc < recoveryCooldown)
    {
      return false;
    }

    return true;
  }

  private async Task<CaptureResult> CaptureStream(
    string deviceName,
    PipeWireStream stream,
    CancellationToken cancellationToken)
  {
    try
    {
      stream = await RefreshStreamIfStale(deviceName, stream, cancellationToken);

      if (!stream.TryGetLatestFrame(out var frameData))
      {
        return CaptureResult.NoChanges(CaptureModePipeWire);
      }

      var info = new SKImageInfo(frameData.Width, frameData.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
      var bitmap = new SKBitmap(info);

      // Copy the frame data into the bitmap to avoid holding onto the pooled buffer
      using (frameData)
      {
        var sourceSpan = frameData.Data.Span;
        var destinationSpan = bitmap.GetPixelSpan();

        var rowBytes = frameData.Width * 4;
        var destinationRowBytes = rowBytes;

        if (frameData.Stride == rowBytes && sourceSpan.Length == destinationSpan.Length)
        {
          sourceSpan.CopyTo(destinationSpan);
        }
        else
        {
          for (var row = 0; row < frameData.Height; row++)
          {
            var sourceOffset = row * frameData.Stride;
            var destinationOffset = row * destinationRowBytes;

            if (sourceOffset + rowBytes > sourceSpan.Length || destinationOffset + rowBytes > destinationSpan.Length)
            {
              throw new InvalidOperationException("PipeWire frame buffer size is invalid for expected dimensions.");
            }

            sourceSpan
              .Slice(sourceOffset, rowBytes)
              .CopyTo(destinationSpan.Slice(destinationOffset, rowBytes));
          }
        }
      }

      return CaptureResult.Ok(bitmap, captureMode: CaptureModePipeWire);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error capturing stream");
      return CaptureResult.Fail(ex);
    }
  }

  private void DisposeStreams()
  {
    foreach (var kv in _streams)
    {
      try { kv.Value.Dispose(); } catch { }
    }
    _streams.Clear();
  }

  private DateTime? GetLastRecoveryAttemptUtc()
  {
    var ticks = Interlocked.Read(ref _lastRecoveryAttemptUtcTicks);
    return ticks > 0
      ? new DateTime(ticks, DateTimeKind.Utc)
      : null;
  }

  private async Task<PipeWireStream> RefreshStreamIfStale(
    string deviceName,
    PipeWireStream stream,
    CancellationToken cancellationToken)
  {
    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
    if (!ShouldRecoverStream(
          nowUtc,
          stream.CreatedUtc,
          stream.LastFrameReceivedUtc,
          _streamStaleThreshold,
          GetLastRecoveryAttemptUtc(),
          _recoveryCooldown))
    {
      return stream;
    }

    if (!await TryRefreshStreams(
          deviceName,
          $"stream has not produced a new frame since {(stream.LastFrameReceivedUtc ?? stream.CreatedUtc):O}",
          cancellationToken))
    {
      return stream;
    }

    if (_streams.TryGetValue(deviceName, out var refreshedStream))
    {
      return refreshedStream;
    }

    return _streams.Values.FirstOrDefault() ?? stream;
  }

  private void ReplaceStreams(IReadOnlyDictionary<string, PipeWireStream> replacementStreams)
  {
    var newStreams = new ConcurrentDictionary<string, PipeWireStream>(replacementStreams);

    foreach (var kv in replacementStreams)
    {
      newStreams[kv.Key] = kv.Value;
    }

    var originalStreams =Interlocked.Exchange(ref _streams, newStreams);

    foreach (var kv in originalStreams)
    {
      try
      {
        kv.Value.Dispose();
      }
      catch
      {
      }
    }
    originalStreams.Clear();
  }

  private async Task<bool> TryRefreshStreams(
    string deviceName,
    string reason,
    CancellationToken cancellationToken)
  {
    if (_disposed)
    {
      return false;
    }

    await _initLock.WaitAsync(cancellationToken);
    try
    {
      var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
      if (GetLastRecoveryAttemptUtc() is { } lastRecoveryAttemptUtc &&
          nowUtc - lastRecoveryAttemptUtc < _recoveryCooldown)
      {
        return false;
      }

      Interlocked.Exchange(ref _lastRecoveryAttemptUtcTicks, nowUtc.Ticks);

      _logger.LogWarning(
        "Refreshing Wayland PipeWire streams for display {DeviceName} because {Reason}.",
        deviceName,
        reason);

      var replacementStreams = await _displayManager.CreatePipeWireStreams(
        forcePortalReinitialize: true,
        cancellationToken: cancellationToken);

      if (replacementStreams.Count == 0)
      {
        _logger.LogWarning("Failed to refresh Wayland PipeWire streams. No replacement streams were created.");
        return false;
      }

      try
      {
        await WaitForStreamsReady(replacementStreams, cancellationToken);
        ReplaceStreams(replacementStreams);
        _isInitialized = !_streams.IsEmpty;

        _logger.LogInformation("Wayland PipeWire streams refreshed successfully.");
        return true;
      }
      catch
      {
        foreach (var stream in replacementStreams.Values)
        {
          stream.Dispose();
        }

        throw;
      }
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error while refreshing Wayland PipeWire streams.");
      return false;
    }
    finally
    {
      _initLock.Release();
    }
  }

  private async Task WaitForStreamsReady(
    IReadOnlyDictionary<string, PipeWireStream> streams,
    CancellationToken cancellationToken)
  {
    foreach (var kv in streams.OrderBy(k => k.Key))
    {
      var deviceName = kv.Key;
      var stream = kv.Value;

      for (var j = 0; j < StreamStartTimeoutMs / StreamStartPollingIntervalMs; j++)
      {
        if (stream.IsStreaming && stream.TryGetLatestFrame(out var frameData))
        {
          frameData.Dispose();
          break;
        }

        await Task.Delay(StreamStartPollingIntervalMs, cancellationToken);
      }

      if (stream.Width > 0 && stream.Height > 0)
      {
        _logger.LogInformation(
          "Stream {Device} initialized with physical dimensions: {Width}x{Height}",
          deviceName,
          stream.Width,
          stream.Height);

        _displayManager.UpdateCaptureSize(deviceName, stream.Width, stream.Height);
      }
      else
      {
        _logger.LogWarning("Stream {Device} did not report physical dimensions before timeout.", deviceName);
      }
    }
  }
}
