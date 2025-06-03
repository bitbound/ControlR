using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Streamer.Extensions;
using ControlR.Streamer.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Streamer.Services;

internal interface IDesktopCapturer
{
  Task ChangeDisplays(string displayId);
  Task<Point> ConvertPercentageLocationToAbsolute(double percentX, double percentY);

  IAsyncEnumerable<ScreenRegionDto> GetChangedRegions();
  IEnumerable<DisplayDto> GetDisplays();

  void ResetDisplays();

  Task StartCapturingChanges();
}

internal class DesktopCapturer : IDesktopCapturer
{
  private readonly TimeSpan _afterFailureDelay = TimeSpan.FromMilliseconds(100);
  private readonly IHostApplicationLifetime _appLifetime;
  private readonly IBitmapUtility _bitmapUtility;
  private readonly ConcurrentQueue<ScreenRegionDto> _changedRegions = new();
  private readonly IDelayer _delayer;
  private readonly ICaptureMetrics _captureMetrics;
  private readonly AutoResetEventAsync _frameReadySignal = new();
  private readonly AutoResetEventAsync _frameRequestedSignal = new(true);
  private readonly ILogger<DesktopCapturer> _logger;
  private readonly IMemoryProvider _memoryProvider;
  private readonly IScreenGrabber _screenGrabber;
  private readonly IOptions<StartupOptions> _startupOptions;
  private readonly IWin32Interop _win32Interop;
  private DisplayInfo[] _displays;
  private bool _forceKeyFrame = true;
  private Bitmap? _lastCpuBitmap;
  private string? _lastDisplayId;
  private Rectangle? _lastMonitorArea;
  private bool _needsKeyFrame = true;
  private DisplayInfo? _selectedDisplay;


  public DesktopCapturer(
    IScreenGrabber screenGrabber,
    IBitmapUtility bitmapUtility,
    IMemoryProvider memoryProvider,
    IWin32Interop win32Interop,
    IDelayer delayer,
    ICaptureMetrics captureMetrics,
    IHostApplicationLifetime appLifetime,
    IOptions<StartupOptions> startupOptions,
    ILogger<DesktopCapturer> logger)
  {
    _screenGrabber = screenGrabber;
    _bitmapUtility = bitmapUtility;
    _memoryProvider = memoryProvider;
    _win32Interop = win32Interop;
    _delayer = delayer;
    _captureMetrics = captureMetrics;
    _startupOptions = startupOptions;
    _appLifetime = appLifetime;
    _logger = logger;
    _displays = [.. _screenGrabber.GetDisplays()];
    _selectedDisplay =
      _displays.FirstOrDefault(x => x.IsPrimary) ??
      _displays.FirstOrDefault();
  }

  public Task ChangeDisplays(string displayId)
  {
    if (_displays.FirstOrDefault(x => x.DeviceName == displayId) is not { } newDisplay)
    {
      _logger.LogWarning("Could not find display with ID {DisplayId} when changing displays.", displayId);
      return Task.CompletedTask;
    }

    _lastCpuBitmap?.Dispose();
    _lastCpuBitmap = null;
    _selectedDisplay = newDisplay;

    return Task.CompletedTask;
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

  public async IAsyncEnumerable<ScreenRegionDto> GetChangedRegions()
  {
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

  public IEnumerable<DisplayDto> GetDisplays()
  {
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

  public void ResetDisplays()
  {
    _displays = [.. _screenGrabber.GetDisplays()];
    _selectedDisplay =
      _displays.FirstOrDefault(x => x.IsPrimary) ??
      _displays.FirstOrDefault();
    _lastCpuBitmap?.Dispose();
    _lastCpuBitmap = null;
    _lastMonitorArea = null;
    _forceKeyFrame = true;
  }

  public Task StartCapturingChanges()
  {
    StartCapturingChangesImpl(_appLifetime.ApplicationStopping).Forget();
    _captureMetrics.Start(_appLifetime.ApplicationStopping);
    return Task.CompletedTask;
  }

  private async Task EncodeCpuCaptureResult(CaptureResult captureResult, int quality, CancellationToken cancellationToken)
  {
    if (!captureResult.IsSuccess)
    {
      return;
    }

    try
    {
      var diffResult = _bitmapUtility.GetChangedArea(captureResult.Bitmap, _lastCpuBitmap);
      if (!diffResult.IsSuccess)
      {
        _logger.LogError(diffResult.Exception, "Failed to get changed area.  Reason: {ErrorReason}", diffResult.Reason);
        await _delayer.Delay(_afterFailureDelay, cancellationToken);
        return;
      }

      var diffArea = diffResult.Value;
      if (diffArea.IsEmpty)
      {
        await _delayer.Delay(_afterFailureDelay, cancellationToken);
        return;
      }

      EncodeRegion(
        bitmap: captureResult.Bitmap,
        region: diffArea,
        quality: quality);
    }
    finally
    {
      _lastCpuBitmap?.Dispose();
      _lastCpuBitmap = (Bitmap)captureResult.Bitmap.Clone();
    }
  }

  private async Task EncodeGpuCaptureResult(CaptureResult captureResult, int quality)
  {
    if (!captureResult.IsSuccess)
    {
      return;
    }

    if (captureResult.DirtyRects.Length == 0)
    {
      await _delayer.Delay(_afterFailureDelay);
      return;
    }

    var bitmapArea = captureResult.Bitmap.ToRectangle();
    foreach (var region in captureResult.DirtyRects)
    {
      if (region.IsEmpty)
      {
        _logger.LogDebug("Skipping empty region.");
        continue;
      }

      var intersect = Rectangle.Intersect(region, bitmapArea);
      if (intersect.IsEmpty)
      {
        continue;
      }

      EncodeRegion(
        bitmap: captureResult.Bitmap,
        region: intersect,
        quality: quality);
    }
  }

  private void EncodeRegion(Bitmap bitmap, Rectangle region, int quality, bool isKeyFrame = false)
  {
    Bitmap? cropped = null;

    try
    {
      using var ms = _memoryProvider.GetRecyclableStream();
      using var writer = new BinaryWriter(ms);

      cropped = _bitmapUtility.CropBitmap(bitmap, region);
      var imageData = _bitmapUtility.EncodeJpeg(cropped, quality);

      var dto = new ScreenRegionDto(
        region.X,
        region.Y,
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

    return _needsKeyFrame && _captureMetrics.Mbps < CaptureMetrics.TargetMbps * .5;
  }

  private async Task StartCapturingChangesImpl(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        await _frameRequestedSignal.Wait(cancellationToken);
        await ThrottleCapturing(cancellationToken);

        _captureMetrics.MarkIteration();

        if (_selectedDisplay is not { } selectedDisplay)
        {
          _logger.LogWarning("Selected display is null.  Unable to capture latest frame.");
          await _delayer.Delay(_afterFailureDelay, cancellationToken);
          continue;
        }

        _win32Interop.SwitchToInputDesktop();

        using var captureResult = _screenGrabber.Capture(selectedDisplay, captureCursor: false);

        if (captureResult.HadNoChanges)
        {
          await _delayer.Delay(_afterFailureDelay, cancellationToken);
          continue;
        }

        if (captureResult.DxTimedOut)
        {
          _logger.LogDebug("DirectX capture timed out. BitBlt fallback used.");
        }

        if (!captureResult.IsSuccess)
        {
          _logger.LogWarning(captureResult.Exception, "Failed to capture latest frame.  Reason: {ResultReason}",
            captureResult.FailureReason);
          ResetDisplays();
          await _delayer.Delay(_afterFailureDelay, cancellationToken);
          continue;
        }

        _captureMetrics.SetIsUsingGpu(captureResult.IsUsingGpu);

        if (ShouldSendKeyFrame())
        {
          EncodeRegion(captureResult.Bitmap, captureResult.Bitmap.ToRectangle(), CaptureMetrics.DefaultImageQuality, isKeyFrame: true);
          _forceKeyFrame = false;
          _needsKeyFrame = false;
          _lastCpuBitmap?.Dispose();
          _lastCpuBitmap = null;
          _lastDisplayId = selectedDisplay.DeviceName;
          _lastMonitorArea = selectedDisplay.MonitorArea;
          continue;
        }

        _needsKeyFrame = _needsKeyFrame || _captureMetrics.IsQualityReduced;

        if (captureResult.IsUsingGpu)
        {
          await EncodeGpuCaptureResult(captureResult, _captureMetrics.Quality);
        }
        else
        {
          await EncodeCpuCaptureResult(captureResult, _captureMetrics.Quality, cancellationToken);
        }

        await _captureMetrics.BroadcastMetrics();
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
  private static Bitmap DownscaleBitmap(Bitmap bitmap, double scale)
  {
    var newWidth = (int)(bitmap.Width * scale);
    var newHeight = (int)(bitmap.Height * scale);
    var resizedBitmap = new Bitmap(newWidth, newHeight);

    using (var graphics = Graphics.FromImage(resizedBitmap))
    {
      graphics.CompositingQuality = CompositingQuality.HighSpeed;
      graphics.InterpolationMode = InterpolationMode.Low;
      graphics.DrawImage(bitmap, 0, 0, newWidth, newHeight);
    }

    return resizedBitmap;
  }
  private async Task ThrottleCapturing(CancellationToken cancellationToken)
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

    await _delayer.WaitForAsync(
      condition: () => _captureMetrics.Mbps < CaptureMetrics.MaxMbps,
      pollingDelay: TimeSpan.FromMilliseconds(10),
      cancellationToken: linkedCts.Token);
  }

}