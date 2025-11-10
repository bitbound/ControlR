using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services;

/// <summary>
/// Responsible for capturing the desktop and streaming it to a consumer.
/// </summary>
public interface IDesktopCapturer : IAsyncDisposable
{
    /// <summary>
    /// Changes the display that is being captured.
    /// </summary>
    /// <param name="displayId">The ID of the display to switch to.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ChangeDisplays(string displayId);

    /// <summary>
    /// Gets an asynchronous stream of captured screen regions.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="ScreenRegionDto"/>.</returns>
    IAsyncEnumerable<ScreenRegionDto> GetCaptureStream(CancellationToken cancellationToken);

    /// <summary>
    /// Forces the next frame to be a key frame.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RequestKeyFrame();

    /// <summary>
    /// Starts the process of capturing screen changes.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartCapturingChanges(CancellationToken cancellationToken);

    /// <summary>
    /// Tries to get the display that is currently selected for capture.
    /// </summary>
    /// <param name="display">When this method returns, contains the selected display, if found; otherwise, null.</param>
    /// <returns>true if a display is selected; otherwise, false.</returns>
    bool TryGetSelectedDisplay([NotNullWhen(true)] out DisplayInfo? display);
}

internal class DesktopCapturer : IDesktopCapturer
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
  private readonly Lock _displayLock = new();
  private readonly IDisplayManager _displayManager;
  private readonly IImageUtility _imageUtility;
  private readonly ILogger<DesktopCapturer> _logger;
  private readonly IMemoryProvider _memoryProvider;
  private readonly TimeSpan _noChangeDelay = TimeSpan.FromMilliseconds(10);
  private readonly IScreenGrabber _screenGrabber;
  private readonly TimeProvider _timeProvider;

  private Task? _captureTask;
  private bool _disposedValue;
  private volatile bool _forceKeyFrame = true;
  private string? _lastDisplayId;
  private Rectangle? _lastMonitorArea;
  private DisplayInfo? _selectedDisplay;

  public DesktopCapturer(
    TimeProvider timeProvider,
    IScreenGrabber screenGrabber,
    IDisplayManager displayManager,
    IMemoryProvider memoryProvider,
    ICaptureMetrics captureMetrics,
    IImageUtility imageUtility,
    IMessenger messenger,
    ILogger<DesktopCapturer> logger)
  {
    _timeProvider = timeProvider;
    _screenGrabber = screenGrabber;
    _displayManager = displayManager;
    _memoryProvider = memoryProvider;
    _captureMetrics = captureMetrics;
    _imageUtility = imageUtility;
    _logger = logger;

    messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged);
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
    return selectedDisplay is null
      ? Point.Empty.AsTaskResult()
      : _displayManager.ConvertPercentageLocationToAbsolute(selectedDisplay.DeviceName, percentX, percentY);
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
    using var ms = _memoryProvider.GetRecyclableStream();
    await using var writer = new BinaryWriter(ms);

    using var cropped = _imageUtility.CropBitmap(bitmap, region);
    var imageData = _imageUtility.EncodeJpeg(cropped, quality);

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
        // If we had a display selected, and it exists still, refresh it.
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

    return _lastMonitorArea != selectedDisplay.MonitorArea;
  }

  private async Task StartCapturingChangesImpl(CancellationToken cancellationToken)
  {
    SKBitmap? previousCapture = null;

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        await _captureMetrics.WaitForBandwidth(cancellationToken);

        // Wait for a space before capturing the screen.  We want the most recent image possible.
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
}
