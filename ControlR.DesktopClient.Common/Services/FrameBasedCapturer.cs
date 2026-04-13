using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Extensions;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.DesktopClient.Common.State;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.WebSocketRelay.Client;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ControlR.DesktopClient.Common.Services;

/// <summary>
/// Orchestrates screen capture by combining <see cref="IScreenGrabber"/> captures
/// with encoding and streaming via channels.
/// </summary>
internal class FrameBasedCapturer : IDesktopCapturer
{
  // ReSharper disable once MemberCanBePrivate.Global
  private readonly TimeSpan _afterFailureDelay = TimeSpan.FromMilliseconds(100);
  private readonly Channel<ScreenRegionsDto> _captureChannel = Channel.CreateBounded<ScreenRegionsDto>(
    new BoundedChannelOptions(capacity: 2)
    {
      SingleReader = true,
      SingleWriter = true,
      FullMode = BoundedChannelFullMode.Wait,
    });
  private readonly SemaphoreSlim _displayLock = new(1, 1);
  private readonly TimeSpan _displayLockTimeout = TimeSpan.FromSeconds(5);
  private readonly IDisplayManager _displayManager;
  private readonly DisposableCollection _disposables = [];
  private readonly IFrameEncoder _frameEncoder;
  private readonly ConcurrentQueue<DateTimeOffset> _framesSent = [];
  private readonly IImageUtility _imageUtility;
  private readonly ILogger<FrameBasedCapturer> _logger;
  private readonly ManualResetEventAsync _maxBandwidthGate = new(isSet: true);
  private readonly TimeSpan _maxBandwidthMonitorDelay = TimeSpan.FromMilliseconds(50);
  private readonly TimeSpan _noChangeDelay = TimeSpan.FromMilliseconds(10);
  private readonly IScreenGrabber _screenGrabber;
  private readonly IRemoteControlSessionState _sessionState;
  private readonly IStreamMetrics _streamMetrics;
  private readonly TimeProvider _timeProvider;

  private Task? _bandwidthMonitorTask;
  private Task? _captureTask;
  private string? _currentCaptureMode;
  private bool _disposedValue;
  private volatile bool _forceKeyFrame = true;
  private Size? _lastCapturePixelSize;
  private string? _lastDisplayId;
  private int _lastEncodedQuality;
  private SKRect? _pendingRecoveryRegion;
  private DisplayInfo? _selectedDisplay;

  public FrameBasedCapturer(
    TimeProvider timeProvider,
    IScreenGrabber screenGrabber,
    IDisplayManager displayManager,
    IImageUtility imageUtility,
    IFrameEncoder frameEncoder,
    IMessenger messenger,
    IRemoteControlSessionState sessionState,
    IStreamMetrics streamMetrics,
    ILogger<FrameBasedCapturer> logger)
  {
    _timeProvider = timeProvider;
    _screenGrabber = screenGrabber;
    _displayManager = displayManager;
    _imageUtility = imageUtility;
    _frameEncoder = frameEncoder;
    _sessionState = sessionState;
    _streamMetrics = streamMetrics;
    _logger = logger;

    _disposables.Add(
      messenger.Register<DisplaySettingsChangedMessage>(this, HandleDisplaySettingsChanged)
    );
  }

  /// <summary>
  /// Changes the current capture target to the specified display.
  /// </summary>
  /// <param name="displayId">The device name of the display to select.</param>
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

    if (_bandwidthMonitorTask is not null)
    {
      await _bandwidthMonitorTask.ConfigureAwait(false);
    }

    _disposables.Dispose();
    _maxBandwidthGate.Dispose();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Returns the name of the capture mode currently in use (e.g., "DirectX" or "GDI").
  /// </summary>
  public string GetCaptureMode() => _currentCaptureMode ?? string.Empty;

  /// <summary>
  /// Streams encoded screen capture regions as an async enumerable.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token to stop the stream.</param>
  /// <returns>An async sequence of DTO wrappers containing screen region data.</returns>
  public async IAsyncEnumerable<DtoWrapper> GetCaptureStream(
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);
    await foreach (var regions in _captureChannel.Reader.ReadAllAsync(cancellationToken))
    {
      yield return DtoWrapper.Create(regions, DtoType.ScreenRegions);
    }
  }

  /// <summary>
  /// Calculates the current frames per second based on sent frames within the given window.
  /// </summary>
  /// <param name="window">The time window to measure FPS over.</param>
  /// <returns>The calculated FPS value.</returns>
  public double GetCurrentFps(TimeSpan window)
  {
    using var acquiredLock = _framesSent.Lock();
    var now = _timeProvider.GetUtcNow();

    while (
      _framesSent.TryPeek(out var timestamp) &&
      timestamp.Add(window) < _timeProvider.GetUtcNow())
    {
      _ = _framesSent.TryDequeue(out _);
    }

    return _framesSent.Count switch
    {
      >= 2 => _framesSent.Count / window.TotalSeconds,
      1 => 1,
      _ => 0
    };
  }

  /// <summary>
  /// Gets the current encoding quality, either from the last encoded frame
  /// or computed from the current bandwidth usage.
  /// </summary>
  /// <returns>The quality value (1-100).</returns>
  public int GetCurrentQuality()
  {
    return _lastEncodedQuality > 0
      ? _lastEncodedQuality
      : GetEffectiveQuality(_streamMetrics.GetMbpsOut());
  }

  /// <summary>
  /// Requests a key frame to be sent on the next capture iteration.
  /// Forces a full frame capture instead of delta updates.
  /// </summary>
  public Task RequestKeyFrame()
  {
    _forceKeyFrame = true;
    return Task.CompletedTask;
  }

  /// <summary>
  /// Starts the capture loop, initializing the screen grabber and beginning
  /// bandwidth monitoring and frame capture tasks.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token to stop capturing.</param>
  public async Task StartCapturingChanges(CancellationToken cancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposedValue, this);

    if (_captureTask is not null)
    {
      return;
    }

    await _screenGrabber.Initialize(cancellationToken);
    _bandwidthMonitorTask = MonitorBandwidth(cancellationToken);
    _captureTask = StartCapturingChangesImpl(cancellationToken);
  }

  /// <summary>
  /// Gets the currently selected display for capture.
  /// </summary>
  /// <returns>A result containing the display info if one is selected.</returns>
  public async Task<Result<DisplayInfo>> TryGetSelectedDisplay()
  {
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    if (_selectedDisplay is { } selected)
    {
      return Result.Ok(selected);
    }
    return Result.Fail<DisplayInfo>("No display selected.");
  }

  /// <summary>
  /// Adds a region to the list if it is non-empty and not already present.
  /// </summary>
  private static SKRect[] AppendRegion(SKRect[] regions, SKRect? additionalRegion)
  {
    if (additionalRegion is not { } region || region.IsEmpty)
    {
      return regions;
    }

    if (Array.Exists(regions, existingRegion => existingRegion.Equals(region)))
    {
      return regions;
    }

    return [.. regions, region];
  }

  /// <summary>
  /// Computes a bounding rectangle that encompasses both input rectangles.
  /// Returns an empty rect if either input is empty.
  /// </summary>
  private static SKRect MergeRegionBounds(SKRect first, SKRect second)
  {
    if (first.IsEmpty)
    {
      return second;
    }

    if (second.IsEmpty)
    {
      return first;
    }

    return new SKRect(
      Math.Min(first.Left, second.Left),
      Math.Min(first.Top, second.Top),
      Math.Max(first.Right, second.Right),
      Math.Max(first.Bottom, second.Bottom));
  }

  /// <summary>
  /// Clears the pending recovery region, indicating it has been sent.
  /// </summary>
  private void ClearPendingRecoveryRegion()
  {
    _pendingRecoveryRegion = null;
  }

  /// <summary>
  /// Encodes bitmap regions in parallel and writes them to the capture channel.
  /// </summary>
  /// <param name="bitmap">The source bitmap containing the capture.</param>
  /// <param name="regions">The regions of the bitmap to encode.</param>
  /// <param name="quality">The JPEG quality level (1-100).</param>
  /// <param name="imageFormat">The image format to encode as.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  private async Task EncodeRegions(
    SKBitmap bitmap,
    SKRect[] regions,
    int quality,
    ImageFormat imageFormat,
    CancellationToken cancellationToken)
  {
    var regionDtos = new ScreenRegionDto[regions.Length];

    Parallel.For(0, regions.Length, index =>
    {
      var region = regions[index];
      var encodedImage = _frameEncoder.EncodeRegion(bitmap, region, quality, imageFormat);

      regionDtos[index] = new ScreenRegionDto(
        region.Left,
        region.Top,
        region.Width,
        region.Height,
        encodedImage,
        imageFormat);
    });

    _lastEncodedQuality = Math.Clamp(quality, 1, 100);
    await _captureChannel.Writer.WriteAsync(new ScreenRegionsDto(regionDtos), cancellationToken);
  }

  /// <summary>
  /// Determines which regions of the bitmap have changed since the previous frame.
  /// Uses DirectX dirty rects when available, otherwise computes pixel diff.
  /// </summary>
  /// <param name="bitmap">The current frame bitmap.</param>
  /// <param name="currentCapture">The capture result with dirty rect info.</param>
  /// <param name="previousFrame">The previous frame bitmap for diff comparison.</param>
  /// <returns>Array of changed regions, or full bitmap rect if unavailable.</returns>
  private SKRect[] GetDirtyRegions(SKBitmap bitmap, CaptureResult currentCapture, SKBitmap? previousFrame)
  {
    if (currentCapture.HadNoChanges)
    {
      return [];
    }

    if (currentCapture.DirtyRects is { } dirtyRects)
    {
      try
      {
        var changedAreas = Array.ConvertAll(dirtyRects, rect => rect.ToSkRect());
        var clampedAreas = _imageUtility.ClampToGridSections(
          new Size(bitmap.Width, bitmap.Height),
          changedAreas);

        if (!clampedAreas.IsSuccess)
        {
          return [bitmap.ToSkRect()];
        }

        return Array.FindAll(clampedAreas.Value, area => !area.IsEmpty);
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Failed to clamp DirectX dirty regions to grid sections.");
        return [bitmap.ToSkRect()];
      }
    }

    if (previousFrame is null)
    {
      return [bitmap.ToSkRect()];
    }

    try
    {
      var diff = _imageUtility.GetChangedAreas(bitmap, previousFrame);
      if (!diff.IsSuccess)
      {
        return [bitmap.ToSkRect()];
      }

      return Array.FindAll(diff.Value, area => !area.IsEmpty);

    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Failed to compute dirty region diff for screen capture.");
      return [bitmap.ToSkRect()];
    }
  }

  /// <summary>
  /// Determines the effective encoding quality based on auto-quality settings
  /// and current bandwidth utilization.
  /// </summary>
  /// <param name="currentMbps">The current outgoing bandwidth in Mbps.</param>
  /// <returns>The quality value to use for encoding (1-100).</returns>
  private int GetEffectiveQuality(double currentMbps)
  {
    if (!_sessionState.IsAutoQualityEnabled)
    {
      return Math.Clamp(_sessionState.ImageQuality, 1, 100);
    }

    var autoQualityMinimum = Math.Clamp(_sessionState.AutoQualityMinimum, 1, 99);
    var autoQualityMaximum = Math.Clamp(_sessionState.AutoQualityMaximum, autoQualityMinimum + 1, 100);
    var lowerThreshold = Math.Max(1d, _sessionState.AutoQualityLowerThresholdMbps);
    var upperThreshold = Math.Max(lowerThreshold + 1d, _sessionState.AutoQualityUpperThresholdMbps);

    if (currentMbps <= lowerThreshold)
    {
      return autoQualityMaximum;
    }

    if (currentMbps >= upperThreshold)
    {
      return autoQualityMinimum;
    }

    var ratio = (currentMbps - lowerThreshold) / (upperThreshold - lowerThreshold);
    return (int)Math.Round(autoQualityMaximum - ((autoQualityMaximum - autoQualityMinimum) * ratio));
  }

  /// <summary>
  /// Computes a recovery region when reduced quality was used for a prior frame.
  /// Returns null if no recovery is needed or bandwidth is still high.
  /// </summary>
  /// <param name="effectiveQuality">The current encoding quality.</param>
  /// <param name="currentMbps">Current outgoing bandwidth.</param>
  /// <returns>The region to send as a recovery frame, or null.</returns>
  private SKRect? GetRecoveryRegion(int effectiveQuality, double currentMbps)
  {
    var autoQualityMaximum = Math.Clamp(
      _sessionState.AutoQualityMaximum,
      Math.Clamp(_sessionState.AutoQualityMinimum, 1, 99) + 1,
      100);

    if (effectiveQuality < autoQualityMaximum ||
        !_sessionState.IsAutoQualityEnabled ||
        _pendingRecoveryRegion is not { } pendingRecoveryRegion ||
        pendingRecoveryRegion.IsEmpty)
    {
      return null;
    }

    var lowerThreshold = Math.Max(1d, _sessionState.AutoQualityLowerThresholdMbps);
    var recoveryThreshold = lowerThreshold * 0.75;

    return currentMbps <= recoveryThreshold
      ? pendingRecoveryRegion
      : null;
  }

  /// <summary>
  /// Gets the currently selected display under a read lock.
  /// </summary>
  private DisplayInfo? GetSelectedDisplay()
  {
    using var locker = _displayLock.AcquireLock(_displayLockTimeout);
    return _selectedDisplay;
  }

  /// <summary>
  /// Handles display settings change events by re-enumerating displays
  /// and updating the selected display reference.
  /// </summary>
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

  /// <summary>
  /// Monitors outgoing bandwidth and controls the max bandwidth gate.
  /// Resets the gate when bandwidth is within limits, blocks when exceeded.
  /// </summary>
  private async Task MonitorBandwidth(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        if (!_sessionState.IsMaxBandwidthEnabled)
        {
          _maxBandwidthGate.Set();
        }
        else if (_streamMetrics.GetMbpsOut() > Math.Max(1d, _sessionState.MaxBandwidthMbps))
        {
          _maxBandwidthGate.Reset();
        }
        else
        {
          _maxBandwidthGate.Set();
        }

        await Task.Delay(_maxBandwidthMonitorDelay, _timeProvider, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        _maxBandwidthGate.Set();
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in bandwidth monitor.");
        _maxBandwidthGate.Set();
        await Task.Delay(_maxBandwidthMonitorDelay, _timeProvider, cancellationToken);
      }
    }
  }

  /// <summary>
  /// Sets the selected display under a write lock.
  /// </summary>
  private void SetSelectedDisplay(DisplayInfo? display)
  {
    using var locker = _displayLock.AcquireLock(_displayLockTimeout);
    _selectedDisplay = display;
  }

  /// <summary>
  /// Determines whether a key frame should be sent based on forced key frame,
  /// display change, or resolution change.
  /// </summary>
  /// <returns>True if a full key frame capture should be performed.</returns>
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

    return _lastCapturePixelSize != selectedDisplay.CapturePixelSize;
  }

  /// <summary>
  /// Main capture loop that coordinates grabbing, encoding, and streaming frames.
  /// Handles bandwidth limiting, key frames, recovery frames, and quality tracking.
  /// </summary>
  private async Task StartCapturingChangesImpl(CancellationToken cancellationToken)
  {
    SKBitmap? previousCapture = null;

    using var dedupeScope = _logger.EnterDedupeScope(cacheDuration: TimeSpan.FromSeconds(5));

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        await _maxBandwidthGate.Wait(cancellationToken);

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
            dedupeScope.LogWarningDeduped("Selected display is null.  Unable to capture latest frame.");
            await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
            continue;
          }

          SetSelectedDisplay(primaryDisplay);
          selectedDisplay = primaryDisplay;
        }

        var currentMbps = _streamMetrics.GetMbpsOut();
        var effectiveQuality = GetEffectiveQuality(currentMbps);

        // Check if we need to send a recovery frame before the normal capture.
        var recoveryRegion = GetRecoveryRegion(effectiveQuality, currentMbps);
        if (recoveryRegion is not null)
        {
          using var recoveryCapture = await _screenGrabber.CaptureDisplay(
            targetDisplay: selectedDisplay,
            captureCursor: _sessionState.CaptureCursor,
            forceKeyFrame: true);

          if (recoveryCapture.IsSuccess)
          {
            await EncodeRegions(
              recoveryCapture.Bitmap,
              [recoveryRegion.Value],
              effectiveQuality,
              ImageFormat.Jpeg,
              cancellationToken);

            previousCapture?.Dispose();
            previousCapture = recoveryCapture.Bitmap.CopyEx();

            ClearPendingRecoveryRegion();
          }

          continue;
        }

        var shouldSendKeyFrame = ShouldSendKeyFrame();

        using var currentCapture = await _screenGrabber.CaptureDisplay(
          targetDisplay: selectedDisplay,
          captureCursor: _sessionState.CaptureCursor,
          forceKeyFrame: shouldSendKeyFrame);

        if (!currentCapture.IsSuccess)
        {
          if (currentCapture.HadNoChanges)
          {
            await Task.Delay(_noChangeDelay, cancellationToken);
            continue;
          }

          dedupeScope.LogWarningDeduped(
            template: "Failed to capture latest frame.  Reason: {ResultReason}",
            exception: currentCapture.Exception,
            args: currentCapture.FailureReason);

          if (currentCapture.HadException)
          {
            await _displayManager.ReloadDisplays();
          }

          await Task.Delay(_afterFailureDelay, _timeProvider, cancellationToken);
          continue;
        }

        if (currentCapture.CaptureMode != _currentCaptureMode)
        {
          _forceKeyFrame = true;
          _currentCaptureMode = currentCapture.CaptureMode;
        }

        var bitmap = currentCapture.Bitmap;
        var bitmapSize = new Size(bitmap.Width, bitmap.Height);

        var dirtyRegions = GetDirtyRegions(bitmap, currentCapture, previousCapture);

        if (dirtyRegions.Length == 0)
        {
          await Task.Delay(_noChangeDelay, cancellationToken);
          continue;
        }

        await EncodeRegions(
          bitmap,
          dirtyRegions,
          effectiveQuality,
          ImageFormat.Jpeg,
          cancellationToken);

        if (effectiveQuality < Math.Clamp(
          _sessionState.AutoQualityMaximum,
          Math.Clamp(_sessionState.AutoQualityMinimum, 1, 99) + 1,
          100))
        {
          TrackReducedQualityRegion(bitmapSize, dirtyRegions);
        }
        
        if (shouldSendKeyFrame)
        {
          _forceKeyFrame = false;
          _lastDisplayId = selectedDisplay.DeviceName;
          _lastCapturePixelSize = selectedDisplay.CapturePixelSize;
        }

        previousCapture?.Dispose();
        previousCapture = bitmap.CopyEx();
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Screen streaming cancelled.");
        break;
      }
      catch (Exception ex)
      {
        dedupeScope.LogErrorDeduped("Error encoding screen captures.", exception: ex);
      }
      finally
      {
        using var guard = _framesSent.Lock();
        _framesSent.Enqueue(_timeProvider.GetUtcNow());
      }
    }
  }

  /// <summary>
  /// Tracks regions that were encoded at reduced quality and need recovery.
  /// Merges overlapping or adjacent reduced-quality regions for efficient recovery.
  /// </summary>
  /// <param name="bitmapSize">The dimensions of the captured bitmap.</param>
  /// <param name="regions">The regions that were encoded at lower quality.</param>
  private void TrackReducedQualityRegion(Size bitmapSize, IEnumerable<SKRect> regions)
  {
    var regionArray = regions.Where(static region => !region.IsEmpty).ToArray();
    if (regionArray.Length == 0)
    {
      return;
    }

    var clampedRegions = _imageUtility.ClampToGridSections(bitmapSize, regionArray);
    if (!clampedRegions.IsSuccess)
    {
      return;
    }

    var mergedRegion = SKRect.Empty;
    foreach (var region in clampedRegions.Value)
    {
      if (region.IsEmpty)
      {
        continue;
      }

      mergedRegion = MergeRegionBounds(mergedRegion, region);
    }

    if (mergedRegion.IsEmpty)
    {
      return;
    }

    _pendingRecoveryRegion = _pendingRecoveryRegion is { } pendingRecoveryRegion
      ? MergeRegionBounds(pendingRecoveryRegion, mergedRegion)
      : mergedRegion;
  }
}