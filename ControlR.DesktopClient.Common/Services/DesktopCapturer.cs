using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopCapturer : IAsyncDisposable
{
  Task ChangeDisplays(string displayId);

  IAsyncEnumerable<ScreenRegionDto> GetCaptureStream(CancellationToken cancellationToken);
  Task RequestKeyFrame();

  Task StartCapturingChanges(CancellationToken cancellationToken);
  bool TryGetSelectedDisplay([NotNullWhen(true)] out DisplayInfo? display);
}

internal class DesktopCapturer : IDesktopCapturer
{
  public const int DefaultImageQuality = 75;

  private readonly TimeSpan _afterFailureDelay = TimeSpan.FromMilliseconds(100);
  private readonly IImageUtility _bitmapUtility;
  private readonly ICaptureMetrics _captureMetrics;
  private readonly Channel<ScreenRegionDto> _captureChannel = Channel.CreateBounded<ScreenRegionDto>(
    new BoundedChannelOptions(capacity: 1)
    {
      SingleReader = true,
      SingleWriter = true,
      FullMode = BoundedChannelFullMode.Wait,
    });
  private readonly Lock _displayLock = new();
  private readonly IDisplayManager _displayManager;
  private readonly ILogger<DesktopCapturer> _logger;
  private readonly IMemoryProvider _memoryProvider;
  private readonly IMessenger _messenger;
  private readonly IImageUtility _imageUtility;
  private readonly TimeSpan _noChangeDelay = TimeSpan.FromMilliseconds(10);
  private readonly IScreenGrabber _screenGrabber;
  private readonly TimeProvider _timeProvider;
  private readonly TimeSpan _waitForBandwidthTimeout = TimeSpan.FromMilliseconds(250);
  private Task? _captureTask;
  private bool _disposedValue;
  private bool _forceKeyFrame = true;
  private string? _lastDisplayId;
  private Rectangle? _lastMonitorArea;
  private DisplayInfo? _selectedDisplay;

  public DesktopCapturer(
    TimeProvider timeProvider,
    IScreenGrabber screenGrabber,
    IDisplayManager displayManager,
    IImageUtility bitmapUtility,
    IMemoryProvider memoryProvider,
    ICaptureMetrics captureMetrics,
    IMessenger messenger,
    IImageUtility imageUtility,
    ILogger<DesktopCapturer> logger)
  {
    _timeProvider = timeProvider;
    _screenGrabber = screenGrabber;
    _displayManager = displayManager;
    _bitmapUtility = bitmapUtility;
    _memoryProvider = memoryProvider;
    _captureMetrics = captureMetrics;
    _messenger = messenger;
    _imageUtility = imageUtility;
    _logger = logger;

    _messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);
  }

  public Task ChangeDisplays(string displayId)
  {
    if (!_displayManager.TryFindDisplay(displayId, out var newDisplay))
    {
      _logger.LogWarning("Could not find display with ID {DisplayId} when changing displays.", displayId);
      return Task.CompletedTask;
    }

    SetSelectedDisplay(newDisplay);
    _forceKeyFrame = true;
    return Task.CompletedTask;
  }

  public Task<Point> ConvertPercentageLocationToAbsolute(double percentX, double percentY)
  {
    var selectedDisplay = GetSelectedDisplay();
    if (selectedDisplay is null)
    {
      return Point.Empty.AsTaskResult();
    }

    return _displayManager.ConvertPercentageLocationToAbsolute(selectedDisplay.DeviceName, percentX, percentY);
  }

  public async ValueTask DisposeAsync()
  {
    if (_disposedValue)
    {
      return;
    }
    _disposedValue = true;

    if (_captureTask is not null)
    {
      await _captureTask.ConfigureAwait(false);
    }

    GC.SuppressFinalize(this);
  }

  public async IAsyncEnumerable<ScreenRegionDto> GetCaptureStream(
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);
    await foreach (var region in _captureChannel.Reader.ReadAllAsync(cancellationToken))
    {
      yield return region;
    }
  }

  public Task<IEnumerable<DisplayDto>> GetDisplays()
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);

    return _displayManager.GetDisplays()
      .ContinueWith(task => task.Result.AsEnumerable(),
        TaskContinuationOptions.ExecuteSynchronously);
  }

  public Task RequestKeyFrame()
  {
    _forceKeyFrame = true;
    return Task.CompletedTask;
  }

  public Task StartCapturingChanges(CancellationToken cancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);
    _captureTask = StartCapturingChangesImpl(cancellationToken);
    return Task.CompletedTask;
  }

  public bool TryGetSelectedDisplay([NotNullWhen(true)] out DisplayInfo? display)
  {
    if (GetSelectedDisplay() is { } selectedDisplay)
    {
      display = selectedDisplay;
      return true;
    }
    display = null;
    return false;
  }

  private async Task EncodeCaptureResult(
    CaptureResult captureResult,
    int quality,
    SKBitmap? previousFrame,
    CancellationToken cancellationToken)
  {
    if (!captureResult.IsSuccess)
    {
      return;
    }

    if (captureResult.DirtyRects is not { } dirtyRects)
    {
      dirtyRects = GetDirtyRects(
        bitmap: captureResult.Bitmap,
        previousFrame: previousFrame);
    }

    // If there are no dirty rects, nothing changed.
    if (dirtyRects.Length == 0)
    {
      await Task.Delay(_noChangeDelay, cancellationToken);
      return;
    }

    var bitmapArea = captureResult.Bitmap.ToRect();
    foreach (var region in dirtyRects)
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

      await EncodeRegion(captureResult.Bitmap, intersect, quality);
    }
  }

  private async Task EncodeRegion(
    SKBitmap bitmap,
    SKRect region,
    int quality,
    bool isKeyFrame = false)
  {
    using var ms = _memoryProvider.GetRecyclableStream();
    using var writer = new BinaryWriter(ms);

    using var cropped = _bitmapUtility.CropBitmap(bitmap, region);
    var imageData = _bitmapUtility.EncodeJpeg(cropped, quality);

    var dto = new ScreenRegionDto(
      region.Left,
      region.Top,
      region.Width,
      region.Height,
      imageData);

    await _captureChannel.Writer.WriteAsync(dto);

    if (!isKeyFrame)
    {
      _captureMetrics.MarkBytesSent(imageData.Length);
    }
  }

  private Rectangle[] GetDirtyRects(SKBitmap bitmap, SKBitmap? previousFrame)
  {
    if (previousFrame is null)
    {
      return [new Rectangle(0, 0, bitmap.Width, bitmap.Height)];
    }

    try
    {
      var diff = _imageUtility.GetChangedArea(bitmap, previousFrame);
      if (!diff.IsSuccess)
      {
        return [new Rectangle(0, 0, bitmap.Width, bitmap.Height)];
      }

      return diff.Value.IsEmpty
        ? []
        : [diff.Value.ToRectangle()];

    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Failed to compute dirty rect diff for X11 virtual capture.");
      return [new Rectangle(0, 0, bitmap.Width, bitmap.Height)];
    }
  }


  private DisplayInfo? GetSelectedDisplay()
  {
    lock (_displayLock)
    {
      _selectedDisplay ??= _displayManager.GetPrimaryDisplay();
      return _selectedDisplay;
    }
  }

  private async Task HandleDisplaySettingsChanged(object subscriber, DisplaySettingsChangedMessage message)
  {
    try
    {
      _logger.LogInformation("Display settings changed. Refreshing display list.");
      await _displayManager.ReloadDisplays();
      lock (_displayLock)
      {
        // If we had a display selected and it exists still, refresh it.
        if (_selectedDisplay is not null &&
            _displayManager.TryFindDisplay(_selectedDisplay.DeviceName, out var selectedDisplay))
        {
          _selectedDisplay = selectedDisplay;
        }
        else
        {
          // Else switch to the primary.
          _selectedDisplay = _displayManager.GetPrimaryDisplay();
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling display settings changed.");
    }
  }

  private void SetSelectedDisplay(DisplayInfo? display)
  {
    lock (_displayLock)
    {
      _selectedDisplay = display;
    }
  }

  private bool ShouldSendKeyFrame()
  {
    if (GetSelectedDisplay() is not { } selectedDisplay)
    {
      return false;
    }

    if (_forceKeyFrame)
    {
      return true;
    }

    if (_lastDisplayId != selectedDisplay.DeviceName)
    {
      return true;
    }

    if (_lastMonitorArea != selectedDisplay.MonitorArea)
    {
      return true;
    }

    return false;
  }

  private async Task StartCapturingChangesImpl(CancellationToken cancellationToken)
  {
    SKBitmap? previousCapture = null;

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        await ThrottleCapturing(cancellationToken);

        // Wait for space before capturing the screen.  We want the most recent image possible.
        if (!await _captureChannel.Writer.WaitToWriteAsync(cancellationToken))
        {
          _logger.LogWarning("Capture channel is closed. Stopping capture.");
          break;
        }

        if (GetSelectedDisplay() is not { } selectedDisplay)
        {
          _logger.LogWarning("Selected display is null.  Unable to capture latest frame.");
          await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
          continue;
        }

        using var currentCapture = _screenGrabber.CaptureDisplay(
              targetDisplay: selectedDisplay,
              captureCursor: false);

        if (currentCapture.HadNoChanges)
        {
          // Nothing changed. Skip encoding.
          await Task.Delay(_noChangeDelay, cancellationToken);
          continue;
        }

        if (!currentCapture.IsSuccess)
        {
          _logger.LogWarning(
            currentCapture.Exception,
            "Failed to capture latest frame.  Reason: {ResultReason}",
            currentCapture.FailureReason);

          await _displayManager.ReloadDisplays();
          await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
          continue;
        }

        if (currentCapture.IsUsingGpu != _captureMetrics.IsUsingGpu)
        {
          // We've switched from GPU to CPU capture, so we need to force a keyframe.
          _forceKeyFrame = true;
        }

        _captureMetrics.SetIsUsingGpu(currentCapture.IsUsingGpu);

        if (ShouldSendKeyFrame())
        {
          await EncodeRegion(
            currentCapture.Bitmap,
            currentCapture.Bitmap.ToRect(),
            DefaultImageQuality,
            isKeyFrame: true);

          _forceKeyFrame = false;
          _lastDisplayId = selectedDisplay.DeviceName;
          _lastMonitorArea = selectedDisplay.MonitorArea;
        }
        else
        {
          await EncodeCaptureResult(
            currentCapture,
            DefaultImageQuality,
            previousCapture,
            cancellationToken);
        }

        previousCapture?.Dispose();
        // This built-in SKBitmap.Copy method has a memory leak.
        previousCapture = currentCapture.Bitmap.Clone();
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
        _captureMetrics.MarkFrameSent();
      }
    }
  }

  private async Task ThrottleCapturing(CancellationToken cancellationToken)
  {
    try
    {
      if (_captureMetrics.Mbps > CaptureMetricsBase.MaxMbps)
      {
        using var cts = new CancellationTokenSource(_waitForBandwidthTimeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
        await _captureMetrics.WaitForBandwidth(linkedCts.Token);
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogDebug("Throttle timed out.");
    }
  }
}