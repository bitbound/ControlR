// Suggested refactoring example for DesktopCapturer.StartCapturingChangesImpl

private async Task StartCapturingChangesImpl(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            await ProcessSingleCaptureFrame(cancellationToken);
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
            HandleFrameCompletion();
        }
    }
}

private async Task ProcessSingleCaptureFrame(CancellationToken cancellationToken)
{
    await _frameRequestedSignal.Wait(cancellationToken);
    await ThrottleCapturing(cancellationToken);

    _captureMetrics.MarkIteration();

    var selectedDisplay = GetSelectedDisplay();
    if (selectedDisplay is null)
    {
        _logger.LogWarning("Selected display is null. Unable to capture latest frame.");
        await Task.Delay(FrameFailureDelay, _timeProvider, cancellationToken);
        return;
    }

    using var captureResult = _screenGrabber.Capture(
        targetDisplay: selectedDisplay,
        captureCursor: false);

    if (!captureResult.IsSuccess)
    {
        await HandleCaptureFailure(captureResult, cancellationToken);
        return;
    }

    if (captureResult.DirtyRects.Length == 0)
    {
        // Nothing changed, short delay
        await Task.Delay(NoChangeDelay, _timeProvider, cancellationToken);
        return;
    }

    await ProcessSuccessfulCapture(captureResult, selectedDisplay);
}

private async Task HandleCaptureFailure(CaptureResult captureResult, CancellationToken cancellationToken)
{
    _logger.LogWarning(
        captureResult.Exception,
        "Failed to capture latest frame. Reason: {ResultReason}",
        captureResult.FailureReason);

    RefreshDisplays();
    await Task.Delay(FrameFailureDelay, _timeProvider, cancellationToken);
}

private async Task ProcessSuccessfulCapture(CaptureResult captureResult, DisplayInfo selectedDisplay)
{
    HandleGpuCaptureModeChange(captureResult);

    if (ShouldSendKeyFrame())
    {
        EncodeKeyFrame(captureResult, selectedDisplay);
        return;
    }

    _needsKeyFrame = _needsKeyFrame || _captureMetrics.IsQualityReduced;
    await EncodeCaptureResult(captureResult, _captureMetrics.Quality);
}

private void HandleFrameCompletion()
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

// Constants to replace magic numbers
private static readonly TimeSpan FrameFailureDelay = TimeSpan.FromMilliseconds(100);
private static readonly TimeSpan NoChangeDelay = TimeSpan.FromMilliseconds(10);
private static readonly TimeSpan ThrottleTimeout = TimeSpan.FromMilliseconds(250);