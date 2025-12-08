using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Common.Services;

internal class FrameBasedCapturer : IDesktopCapturer
{
  // ReSharper disable once MemberCanBePrivate.Global
  public const int DefaultImageQuality = 75;

  private readonly TimeSpan _afterFailureDelay = TimeSpan.FromMilliseconds(100);
  private readonly Channel<ScreenRegionDto> _captureChannel = Channel.CreateBounded<ScreenRegionDto>(
    new BoundedChannelOptions(capacity: 1)
    {
      SingleReader = true,
      SingleWriter = true,
      FullMode = BoundedChannelFullMode.Wait,
    });
  private readonly ICaptureMetrics _captureMetrics;
  private readonly SemaphoreSlim _displayLock = new(1, 1);
  private readonly TimeSpan _displayLockTimeout = TimeSpan.FromSeconds(5);
  private readonly IDisplayManager _displayManager;
  private readonly IFrameEncoder _frameEncoder;
  private readonly IImageUtility _imageUtility;
  private readonly ILogger<FrameBasedCapturer> _logger;
  private readonly TimeSpan _noChangeDelay = TimeSpan.FromMilliseconds(10);
  private readonly IScreenGrabber _screenGrabber;
  private readonly TimeProvider _timeProvider;

  private Task? _captureTask;
  private bool _disposedValue;
  private volatile bool _forceKeyFrame = true;
  private string? _lastDisplayId;
  private Rectangle? _lastMonitorArea;
  private DisplayInfo? _selectedDisplay;

  public FrameBasedCapturer(
    TimeProvider timeProvider,
    IScreenGrabber screenGrabber,
    IDisplayManager displayManager,
    ICaptureMetrics captureMetrics,
    IImageUtility imageUtility,
    IFrameEncoder frameEncoder,
    IMessenger messenger,
    ILogger<FrameBasedCapturer> logger)
  {
    _timeProvider = timeProvider;
    _screenGrabber = screenGrabber;
    _displayManager = displayManager;
    _captureMetrics = captureMetrics;
    _imageUtility = imageUtility;
    _frameEncoder = frameEncoder;
    _logger = logger;

    messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);
  }

  public async Task ChangeDisplays(string displayId)
  {
    var findResult = await _displayManager.TryFindDisplay(displayId);
    if (!findResult.IsSuccess)
    {
      _logger.LogWarning("Could not find display with ID {DisplayId} when changing displays.", displayId);
      return;
    }

    SetSelectedDisplay(findResult.Value);
    _forceKeyFrame = true;
  }

  public async Task<Point> ConvertPercentageLocationToAbsolute(double percentX, double percentY)
  {
    var selectedDisplay = GetSelectedDisplay();
    if (selectedDisplay is null)
    {
      var primary = await _displayManager.GetPrimaryDisplay();
      if (primary is null)
      {
        return Point.Empty;
      }
      SetSelectedDisplay(primary);
      selectedDisplay = primary;
    }

    return await _displayManager.ConvertPercentageLocationToAbsolute(selectedDisplay.DeviceName, percentX, percentY);
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

  public async IAsyncEnumerable<DtoWrapper> GetCaptureStream(
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);
    await foreach (var region in _captureChannel.Reader.ReadAllAsync(cancellationToken))
    {
      yield return DtoWrapper.Create(region, DtoType.ScreenRegion);
    }
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

  public async Task<Result<DisplayInfo>> TryGetSelectedDisplay()
  {
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    if (_selectedDisplay is {} selected){
      return Result.Ok(selected);
    }
    return Result.Fail<DisplayInfo>("No display selected.");
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

    var bitmapArea = captureResult.Bitmap.ToSkRect();
    SKRect dirtyRect;

    if (captureResult.DirtyRects is { } dirtyRects)
    {
      dirtyRect = dirtyRects.Aggregate(SKRect.Empty, (current, rect) => SKRect.Union(current, rect.ToSkRect()));
      SKRect.Intersect(dirtyRect, bitmapArea);
    }
    else
    {
      dirtyRect = GetDirtyRect(captureResult.Bitmap, previousFrame);
    }

    // If there are no dirty rects, nothing changed.
    if (dirtyRect.IsEmpty)
    {
      await Task.Delay(_noChangeDelay, cancellationToken);
      return;
    }

    await EncodeRegion(captureResult.Bitmap, dirtyRect, quality);
  }

  private async Task EncodeRegion(
    SKBitmap bitmap,
    SKRect region,
    int quality,
    bool isKeyFrame = false)
  {
    var encodedImage = _frameEncoder.EncodeRegion(bitmap, region, quality);

    var dto = new ScreenRegionDto(
      region.Left,
      region.Top,
      region.Width,
      region.Height,
      encodedImage);

    await _captureChannel.Writer.WriteAsync(dto);

    if (!isKeyFrame)
    {
      _captureMetrics.MarkBytesSent(encodedImage.Length);
    }
  }

  private SKRect GetDirtyRect(SKBitmap bitmap, SKBitmap? previousFrame)
  {
    if (previousFrame is null)
    {
      return new SKRect(0, 0, bitmap.Width, bitmap.Height);
    }

    try
    {
      var diff = _imageUtility.GetChangedArea(bitmap, previousFrame);
      if (!diff.IsSuccess)
      {
        return new SKRect(0, 0, bitmap.Width, bitmap.Height);
      }

      return diff.Value.IsEmpty
        ? SKRect.Empty
        : diff.Value;

    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Failed to compute dirty rect diff for X11 virtual capture.");
      return new SKRect(0, 0, bitmap.Width, bitmap.Height);
    }
  }

  private DisplayInfo? GetSelectedDisplay()
  {
    using var locker = _displayLock.AcquireLock(_displayLockTimeout);
    lock (_displayLock)
    {
      return _selectedDisplay;
    }
  }

  private async Task HandleDisplaySettingsChanged(object subscriber, DisplaySettingsChangedMessage message)
  {
    try
    {
      _logger.LogInformation("Display settings changed. Refreshing display list.");
      await _displayManager.ReloadDisplays();

      using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);

      if (_selectedDisplay is not null)
      {
        var findResult = await _displayManager.TryFindDisplay(_selectedDisplay.DeviceName);
        if (findResult.IsSuccess)
        {
          _selectedDisplay = findResult.Value;
          return;
        }
      }

      _selectedDisplay = await _displayManager.GetPrimaryDisplay();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling display settings changed.");
    }
  }

  private void SetSelectedDisplay(DisplayInfo? display)
  {
    using var locker = _displayLock.AcquireLock(_displayLockTimeout);
    _selectedDisplay = display;
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

    return _lastMonitorArea != selectedDisplay.MonitorArea;
  }

  private async Task StartCapturingChangesImpl(CancellationToken cancellationToken)
  {
    SKBitmap? previousCapture = null;

    try
    {
      await _screenGrabber.Initialize(cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize screen grabber.");
      return;
    }

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        // Wait for a space before capturing the screen.  We want the most recent image possible.
        if (!await _captureChannel.Writer.WaitToWriteAsync(cancellationToken))
        {
          _logger.LogWarning("Capture channel is closed. Stopping capture.");
          break;
        }

        var selectedDisplay = GetSelectedDisplay();
        if (selectedDisplay is null)
        {
          var primaryDisplay = await _displayManager.GetPrimaryDisplay();
          if (primaryDisplay is null)
          {
            _logger.LogWarning("Selected display is null.  Unable to capture latest frame.");
            await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
            continue;
          }

          SetSelectedDisplay(primaryDisplay);
          selectedDisplay = primaryDisplay;
        }

          using var currentCapture = await _screenGrabber.CaptureDisplay(
              targetDisplay: selectedDisplay,
              captureCursor: false,
              forceKeyFrame: _forceKeyFrame);

        if (currentCapture.HadNoChanges && !_forceKeyFrame)
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

          if (currentCapture.HadException)
          {
            await _displayManager.ReloadDisplays();
          }

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
            currentCapture.Bitmap.ToSkRect(),
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
        // The built-in SKBitmap.Copy method has a memory leak.
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
}
