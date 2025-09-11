using System.Collections.Concurrent;
using System.Drawing;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopCapturer : IAsyncDisposable
{

  DisplayInfo? SelectedDisplay { get; }
  Task ChangeDisplays(string displayId);
  Task<Point> ConvertPercentageLocationToAbsolute(double percentX, double percentY);

  IAsyncEnumerable<ScreenRegionDto> GetChangedRegions();
  Task<IEnumerable<DisplayDto>> GetDisplays();
  Task RequestKeyFrame();
  Task ResetDisplays();

  Task StartCapturingChanges();
}

internal class DesktopCapturer : IDesktopCapturer
{
  private readonly TimeSpan _afterFailureDelay = TimeSpan.FromMilliseconds(100);
  private readonly IHostApplicationLifetime _appLifetime;
  private readonly IImageUtility _bitmapUtility;
  private readonly ICaptureMetrics _captureMetrics;
  private readonly ConcurrentQueue<ScreenRegionDto> _changedRegions = new();
  private readonly SemaphoreSlim _displayLock = new(1, 1);
  private readonly AutoResetEventAsync _frameReadySignal = new();
  private readonly AutoResetEventAsync _frameRequestedSignal = new(true);
  private readonly ILogger<DesktopCapturer> _logger;
  private readonly IMemoryProvider _memoryProvider;
  private readonly IScreenGrabber _screenGrabber;
  private readonly TimeProvider _timeProvider;
  private CancellationTokenSource? _captureCts;
  private Task? _captureTask;
  private DisplayInfo[] _displays;
  private bool _disposedValue;
  private bool _forceKeyFrame = true;
  private string? _lastDisplayId;
  private Rectangle? _lastMonitorArea;
  private bool _needsKeyFrame = true;
  private DisplayInfo? _selectedDisplay;

  public DesktopCapturer(
    TimeProvider timeProvider,
    IScreenGrabber screenGrabber,
    IImageUtility bitmapUtility,
    IMemoryProvider memoryProvider,
    ICaptureMetrics captureMetrics,
    IHostApplicationLifetime appLifetime,
    ILogger<DesktopCapturer> logger)
  {
    _timeProvider = timeProvider;
    _screenGrabber = screenGrabber;
    _bitmapUtility = bitmapUtility;
    _memoryProvider = memoryProvider;
    _captureMetrics = captureMetrics;
    _appLifetime = appLifetime;
    _logger = logger;
    _displays = [.. _screenGrabber.GetDisplays()];
    _selectedDisplay =
      _displays.FirstOrDefault(x => x.IsPrimary) ??
      _displays.FirstOrDefault();
  }

  public DisplayInfo? SelectedDisplay => _selectedDisplay;

  public async Task ChangeDisplays(string displayId)
  {
    using var displayLock = await _displayLock.AcquireLock(_appLifetime.ApplicationStopping);

    if (_displays.FirstOrDefault(x => x.DeviceName == displayId) is not { } newDisplay)
    {
      _logger.LogWarning("Could not find display with ID {DisplayId} when changing displays.", displayId);
      return;
    }

    _selectedDisplay = newDisplay;
    _forceKeyFrame = true;
  }

  public Task<Point> ConvertPercentageLocationToAbsolute(double percentX, double percentY)
  {
    if (_selectedDisplay?.MonitorArea is not { } bounds)
    {
      return Point.Empty.AsTaskResult();
    }

    var absoluteX = bounds.Width * percentX + bounds.Left;
    var absoluteY = bounds.Height * percentY + bounds.Top;
    return new Point((int)absoluteX, (int)absoluteY).AsTaskResult();
  }

  public async ValueTask DisposeAsync()
  {
    if (_disposedValue)
    {
      return;
    }
    _disposedValue = true;

    if (_captureCts is not null)
    {
      await _captureCts.CancelAsync();
    }

    if (_captureTask is not null)
    {
      await _captureTask.ConfigureAwait(false);
    }

    Disposer.DisposeAll(
      _frameReadySignal,
      _frameRequestedSignal,
      _captureCts);

    GC.SuppressFinalize(this);
  }

  public async IAsyncEnumerable<ScreenRegionDto> GetChangedRegions()
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);
    try
    {
      await _frameReadySignal.Wait(_appLifetime.ApplicationStopping);

      while (_changedRegions.TryDequeue(out var region))
      {
        yield return region;
      }
      _captureMetrics.MarkFrameSent();
    }
    finally
    {
      _frameRequestedSignal.Set();
    }
  }

  public async Task<IEnumerable<DisplayDto>> GetDisplays()
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);

    using var displayLock = await _displayLock.AcquireLock(_appLifetime.ApplicationStopping);

    return _displays
      .Select(x => new DisplayDto
      {
        DisplayId = x.DeviceName,
        Height = x.MonitorArea.Height,
        IsPrimary = x.IsPrimary,
        Width = x.MonitorArea.Width,
        Name = x.DisplayName,
        Left = x.MonitorArea.Left,
        ScaleFactor = x.ScaleFactor,
      });
  }

  public Task RequestKeyFrame()
  {
    _forceKeyFrame = true;
    return Task.CompletedTask;
  }

  public async Task ResetDisplays()
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);

    using var displayLock = await _displayLock.AcquireLock(_appLifetime.ApplicationStopping);
  }

  public Task StartCapturingChanges()
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);

    _captureCts = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopping);
    _captureTask = StartCapturingChangesImpl(_captureCts.Token);
    _captureMetrics.Start(_captureCts.Token);
    return Task.CompletedTask;
  }

  private static SKBitmap DownscaleBitmap(SKBitmap bitmap, double scale)
  {
    var newWidth = (int)(bitmap.Width * scale);
    var newHeight = (int)(bitmap.Height * scale);
    var imageInfo = new SKImageInfo(newWidth, newHeight);
    return bitmap.Resize(imageInfo, default(SKSamplingOptions));
  }

  private Task EncodeCaptureResult(CaptureResult captureResult, int quality)
  {
    if (!captureResult.IsSuccess)
    {
      return Task.CompletedTask;
    }

    // If there are no dirty rects, nothing changed.
    if (captureResult.DirtyRects.Length == 0)
    {
      return Task.CompletedTask;
    }

    var bitmapArea = captureResult.Bitmap.ToRect();
    foreach (var region in captureResult.DirtyRects)
    {
      if (region.IsEmpty)
      {
        _logger.LogDebug("Skipping empty region.");
        continue;
      }

      var intersect = SKRect.Intersect(region.ToRect(), bitmapArea);
      if (intersect.IsEmpty)
      {
        continue;
      }

      EncodeRegion(captureResult.Bitmap, intersect, quality);
    }
    return Task.CompletedTask;
  }

  private void EncodeRegion(SKBitmap bitmap, SKRect region, int quality, bool isKeyFrame = false)
  {
    SKBitmap? cropped = null;

    try
    {
      using var ms = _memoryProvider.GetRecyclableStream();
      using var writer = new BinaryWriter(ms);

      cropped = _bitmapUtility.CropBitmap(bitmap, region);
      var imageData = _bitmapUtility.EncodeJpeg(cropped, quality);

      var dto = new ScreenRegionDto(
        region.Left,
        region.Top,
        region.Width,
        region.Height,
        imageData);

      _changedRegions.Enqueue(dto);

      if (!isKeyFrame)
      {
        _captureMetrics.MarkBytesSent(imageData.Length);
      }
    }
    finally
    {
      cropped?.Dispose();
    }
  }

  private void ResetDisplaysImpl()
  {
    _displays = [.. _screenGrabber.GetDisplays()];
    _selectedDisplay =
      _displays.FirstOrDefault(x => x.IsPrimary) ??
      _displays.FirstOrDefault();
    _lastMonitorArea = null;
    _forceKeyFrame = true;
  }

  private bool ShouldSendKeyFrame()
  {
    if (_selectedDisplay is null)
    {
      return false;
    }

    if (_forceKeyFrame)
    {
      return true;
    }

    if (_lastDisplayId != _selectedDisplay.DeviceName)
    {
      return true;
    }

    if (_lastMonitorArea != _selectedDisplay.MonitorArea)
    {
      return true;
    }

    return _needsKeyFrame && _captureMetrics.Mbps < CaptureMetricsBase.TargetMbps * .5;
  }

  private async Task StartCapturingChangesImpl(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      using var displayLock = await _displayLock.AcquireLock(_appLifetime.ApplicationStopping);

      try
      {
        await _frameRequestedSignal.Wait(cancellationToken);
        await ThrottleCapturing(cancellationToken);

        _captureMetrics.MarkIteration();

        if (_selectedDisplay is not { } selectedDisplay)
        {
          _logger.LogWarning("Selected display is null.  Unable to capture latest frame.");
          await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
          continue;
        }

        using var captureResult = _screenGrabber.Capture(
              targetDisplay: selectedDisplay,
              captureCursor: false);

        if (captureResult.IsSuccess && captureResult.DirtyRects.Length == 0)
        {
          // Nothing changed, so skip encoding.
          await Task.Delay(TimeSpan.FromMilliseconds(10), _timeProvider, cancellationToken);
          continue;
        }

        if (!captureResult.IsSuccess)
        {
          _logger.LogWarning(
            captureResult.Exception,
            "Failed to capture latest frame.  Reason: {ResultReason}",
            captureResult.FailureReason);

          ResetDisplaysImpl();
          await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
          continue;
        }

        if (captureResult.IsUsingGpu != _captureMetrics.IsUsingGpu)
        {
          // We've switched from GPU to CPU capture, so we need to force a keyframe.
          _forceKeyFrame = true;
        }

        _captureMetrics.SetIsUsingGpu(captureResult.IsUsingGpu);

        if (ShouldSendKeyFrame())
        {
          EncodeRegion(captureResult.Bitmap, captureResult.Bitmap.ToRect(), CaptureMetricsBase.DefaultImageQuality, isKeyFrame: true);
          _forceKeyFrame = false;
          _needsKeyFrame = false;
          _lastDisplayId = selectedDisplay.DeviceName;
          _lastMonitorArea = selectedDisplay.MonitorArea;
          continue;
        }

        _needsKeyFrame = _needsKeyFrame || _captureMetrics.IsQualityReduced;

  await EncodeCaptureResult(captureResult, _captureMetrics.Quality);
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Screen streaming cancelled.");
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error encoding screen captures.");
      }
      finally
      {
        if (!_changedRegions.IsEmpty)
        {
          _frameReadySignal.Set();
        }
        else
        {
          _frameRequestedSignal.Set();
        }
      }
    }
  }

  private async Task StartCapturingSession0(CancellationToken cancellationToken)
  {
    _captureMetrics.SetIsUsingGpu(false);
    var bounds = _screenGrabber.GetVirtualScreenBounds();

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        if (_selectedDisplay is not { } selectedDisplay)
        {
          _logger.LogWarning("Selected display is null.  Unable to capture latest frame.");
          await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
          continue;
        }

        await _frameRequestedSignal.Wait(cancellationToken);
        await ThrottleCapturing(cancellationToken);

        _captureMetrics.MarkIteration();

        //using var captureResult = _screenGrabber.CaptureSession0Desktop();
        using var captureResult = _screenGrabber.Capture(captureCursor: false);

        if (!captureResult.IsSuccess)
        {
          _logger.LogWarning(
            captureResult.Exception,
            "Failed to capture latest frame.  Reason: {ResultReason}",
            captureResult.FailureReason);

          await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
          continue;
        }

        if (ShouldSendKeyFrame())
        {
          EncodeRegion(captureResult.Bitmap, bounds.ToRect(), CaptureMetricsBase.DefaultImageQuality, isKeyFrame: true);
          _forceKeyFrame = false;
          _needsKeyFrame = false;
          _lastDisplayId = selectedDisplay.DeviceName;
          _lastMonitorArea = selectedDisplay.MonitorArea;
          continue;
        }

        _needsKeyFrame = _needsKeyFrame || _captureMetrics.IsQualityReduced;
  await EncodeCaptureResult(captureResult, _captureMetrics.Quality);
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Screen streaming cancelled.");
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error encoding screen captures.");
      }
      finally
      {
        if (!_changedRegions.IsEmpty)
        {
          _frameReadySignal.Set();
        }
        else
        {
          _frameRequestedSignal.Set();
        }
      }
    }
  }
  private async Task ThrottleCapturing(CancellationToken cancellationToken)
  {
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250), _timeProvider);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
      await _captureMetrics.WaitForBandwidth(linkedCts.Token);
    }
    catch (OperationCanceledException)
    {
      _logger.LogDebug("Throttle timed out.");
    }
  }
}