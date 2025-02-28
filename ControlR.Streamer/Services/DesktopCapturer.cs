using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using Bitbound.SimpleMessenger;
using ControlR.Libraries.ScreenCapture.Extensions;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Streamer.Messages;
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
  private const int DefaultImageQuality = 75;
  private const int MinimumQuality = 20;
  private const double TargetMbps = 5;
  private readonly TimeSpan _afterFailureDelay = TimeSpan.FromMilliseconds(100);
  private readonly IHostApplicationLifetime _appLifetime;
  private readonly IBitmapUtility _bitmapUtility;
  private readonly ConcurrentQueue<ScreenRegionDto> _changedRegions = new();
  private readonly IDelayer _delayer;
  private readonly AutoResetEventAsync _frameReadySignal = new();
  private readonly AutoResetEventAsync _frameRequestedSignal = new(true);
  private readonly ILogger<DesktopCapturer> _logger;
  private readonly IMemoryProvider _memoryProvider;
  private readonly IMessenger _messenger;
  private readonly Stopwatch _metricsBroadcastTimer = Stopwatch.StartNew();
  private readonly IScreenGrabber _screenGrabber;
  private readonly ConcurrentQueue<SentFrame> _sentRegions = new();
  private readonly IOptions<StartupOptions> _startupOptions;
  private readonly TimeProvider _timeProvider;
  private readonly IWin32Interop _win32Interop;
  private volatile int _cpuFrames;
  private double _currentMbps;
  private int _currentQuality = DefaultImageQuality;
  private DisplayInfo[] _displays;
  private bool _forceKeyFrame = true;
  private long _frameCount;
  private volatile int _gpuFrames;
  private int _iterations;
  private Bitmap? _lastCpuBitmap;
  private string? _lastDisplayId;
  private Rectangle? _lastMonitorArea;
  private bool _needsKeyFrame = true;
  private DisplayInfo? _selectedDisplay;


  public DesktopCapturer(
    TimeProvider timeProvider,
    IMessenger messenger,
    IScreenGrabber screenGrabber,
    IBitmapUtility bitmapUtility,
    IMemoryProvider memoryProvider,
    IWin32Interop win32Interop,
    IDelayer delayer,
    IHostApplicationLifetime appLifetime,
    IOptions<StartupOptions> startupOptions,
    ILogger<DesktopCapturer> logger)
  {
    _messenger = messenger;
    _screenGrabber = screenGrabber;
    _bitmapUtility = bitmapUtility;
    _memoryProvider = memoryProvider;
    _win32Interop = win32Interop;
    _timeProvider = timeProvider;
    _delayer = delayer;
    _startupOptions = startupOptions;
    _appLifetime = appLifetime;
    _logger = logger;
    _displays = _screenGrabber.GetDisplays().ToArray();
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
    _displays = _screenGrabber.GetDisplays().ToArray();
    _selectedDisplay =
      _displays.FirstOrDefault(x => x.IsPrimary) ??
      _displays.FirstOrDefault();
    _lastCpuBitmap?.Dispose();
    _lastCpuBitmap = null;
    _forceKeyFrame = true;
  }

  public Task StartCapturingChanges()
  {
    StartCapturingChangesImpl(_appLifetime.ApplicationStopping).Forget();
    return Task.CompletedTask;
  }

  private async Task EncodeCpuCaptureResult(CaptureResult captureResult, CancellationToken stoppingToken)
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
        await _delayer.Delay(_afterFailureDelay, stoppingToken);
        return;
      }

      var diffArea = diffResult.Value;
      if (diffArea.IsEmpty)
      {
        await _delayer.Delay(_afterFailureDelay, stoppingToken);
        return;
      }

      EncodeRegion(captureResult.Bitmap, diffArea);
      Interlocked.Increment(ref _cpuFrames);
    }
    finally
    {
      _lastCpuBitmap?.Dispose();
      _lastCpuBitmap = (Bitmap)captureResult.Bitmap.Clone();
    }
  }

  private async Task EncodeGpuCaptureResult(CaptureResult captureResult)
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

      EncodeRegion(captureResult.Bitmap, intersect);
    }

    Interlocked.Increment(ref _gpuFrames);
  }

  private void EncodeRegion(Bitmap bitmap, Rectangle region, bool isKeyFrame = false)
  {
    Bitmap? cropped = null;

    try
    {
      var quality = isKeyFrame ? DefaultImageQuality : _currentQuality;

      using var ms = _memoryProvider.GetRecyclableStream();
      using var writer = new BinaryWriter(ms);

      cropped = _bitmapUtility.CropBitmap(bitmap, region);
      var imageData = _bitmapUtility.EncodeJpeg(cropped, quality);

      var dto = new ScreenRegionDto(
        _startupOptions.Value.SessionId,
        region.X,
        region.Y,
        region.Width,
        region.Height,
        imageData);

      _changedRegions.Enqueue(dto);

      if (!isKeyFrame)
      {
        _sentRegions.Enqueue(new SentFrame(imageData.Length, _timeProvider.GetLocalNow()));
      }
    }
    finally
    {
      cropped?.Dispose();
    }
  }

  private int GetTargetImageQuality()
  {
    if (_currentMbps < TargetMbps)
    {
      return DefaultImageQuality;
    }

    var quality = (int)(TargetMbps / _currentMbps * DefaultImageQuality);
    var newQuality = Math.Max(quality, MinimumQuality);

    if (newQuality < _currentQuality)
    {
      return newQuality;
    }

    return Math.Min(_currentQuality + 2, newQuality);
  }

  private Task ProcessMetrics()
  {
    // Don't do processing evey frame.
    if (_frameCount++ % 5 != 0)
    {
      return Task.CompletedTask;
    }

    _currentQuality = GetTargetImageQuality();
    _needsKeyFrame = _needsKeyFrame || _currentQuality < DefaultImageQuality;

    // Keep only frames in our sample window.
    while (
      _sentRegions.TryPeek(out var frame) &&
      frame.Timestamp.AddSeconds(20) < _timeProvider.GetLocalNow())
    {
      _sentRegions.TryDequeue(out _);
    }

    if (_sentRegions.Count >= 2)
    {
      var sampleSpan = _timeProvider.GetLocalNow() - _sentRegions.First().Timestamp;
      _currentMbps = _sentRegions.Sum(x => x.Size) / 1024.0 / 1024.0 / sampleSpan.TotalSeconds * 8;
    }
    else if (_sentRegions.Count == 1)
    {
      _currentMbps = _sentRegions.First().Size / 1024.0 / 1024.0 * 8;
    }
    else
    {
      return Task.CompletedTask;
    }

    return Task.CompletedTask;
  }

  private async Task ReportMetrics()
  {
    await ProcessMetrics();
    await ThrottleStream();

    if (_metricsBroadcastTimer.Elapsed.TotalSeconds > 3)
    {
      var gpuFps = _gpuFrames / _metricsBroadcastTimer.Elapsed.TotalSeconds;
      var cpuFps = _cpuFrames / _metricsBroadcastTimer.Elapsed.TotalSeconds;
      var ips = _iterations / _metricsBroadcastTimer.Elapsed.TotalSeconds;

      _metricsBroadcastTimer.Restart();
      _logger.LogDebug(
        "Mbps: {CurrentMbps:N2} | GPU FPS: {GpuFps:N2} | CPU FPS: {CpuFps:N2} | IPS (iterations): {IPS:N2} | Current Quality: {ImageQuality}",
        _currentMbps,
        gpuFps,
        cpuFps,
        ips,
        _currentQuality);

      await _messenger.Send(new DisplayMetricsChangedMessage(_currentMbps, gpuFps, cpuFps, ips, _currentQuality));
      Interlocked.Exchange(ref _gpuFrames, 0);
      Interlocked.Exchange(ref _cpuFrames, 0);
      Interlocked.Exchange(ref _iterations, 0);
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

    return _needsKeyFrame && _currentMbps < TargetMbps * .5;
  }

  private async Task StartCapturingChangesImpl(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await _frameRequestedSignal.Wait(stoppingToken);

        Interlocked.Increment(ref _iterations);

        if (_selectedDisplay is not { } selectedDisplay)
        {
          _logger.LogWarning("Selected display is null.  Unable to capture latest frame.");
          await _delayer.Delay(_afterFailureDelay, stoppingToken);
          continue;
        }

        _win32Interop.SwitchToInputDesktop();

        using var captureResult = _screenGrabber.Capture(selectedDisplay);

        if (captureResult.HadNoChanges)
        {
          await _delayer.Delay(_afterFailureDelay, stoppingToken);
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
          _lastCpuBitmap = null;
          _forceKeyFrame = true;
          await _delayer.Delay(_afterFailureDelay, stoppingToken);
          continue;
        }

        if (ShouldSendKeyFrame())
        {
          EncodeRegion(captureResult.Bitmap, captureResult.Bitmap.ToRectangle(), true);
          _forceKeyFrame = false;
          _needsKeyFrame = false;
          _lastCpuBitmap?.Dispose();
          _lastCpuBitmap = null;
          _lastDisplayId = selectedDisplay.DeviceName;
          _lastMonitorArea = selectedDisplay.MonitorArea;
          continue;
        }

        // Bypass for now, as having too many small changes can
        // result in worse performance on the front-end when
        // drawing to the canvas.  Might need to find the break-even
        // point on number of regions.
        //if (captureResult.IsUsingGpu)
        //{
        //  await EncodeGpuCaptureResult(captureResult);
        //}
        //else
        //{
        //  await EncodeCpuCaptureResult(captureResult, stoppingToken);
        //}

        await EncodeCpuCaptureResult(captureResult, stoppingToken);

        await ReportMetrics();
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

  private async Task ThrottleStream()
  {
    if (_currentMbps > TargetMbps * 2)
    {
      var waitMs = Math.Min(100, _currentMbps / TargetMbps * 10);
      var waitTime = TimeSpan.FromMilliseconds(waitMs);
      await _delayer.Delay(waitTime);
    }
  }

  private record SentFrame(int Size, DateTimeOffset Timestamp);
}