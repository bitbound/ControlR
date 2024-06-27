
using Bitbound.SimpleMessenger;
using ControlR.Libraries.ScreenCapture.Extensions;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Streamer.Messages;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;

namespace ControlR.Streamer.Services;

internal interface IDisplayManager
{
    Task ChangeDisplays(string displayId);
    Task<Point> ConvertPercentageLocationToAbsolute(double percentX, double percentY);

    IAsyncEnumerable<byte[]> GetChangedRegions();
    IEnumerable<DisplayDto> GetDisplays();

    void ResetDisplays();

    Task StartCapturingChanges(ViewerReadyForStreamDto viewerReadyDto);
}


internal class DisplayManager : IDisplayManager
{
    private const int _defaultImageQuality = 75;
    private const int _minimumQuality = 20;
    private const double _targetMbps = 5;
    private readonly TimeSpan _afterFailureDelay = TimeSpan.FromMilliseconds(100);
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IBitmapUtility _bitmapUtility;
    private readonly ConcurrentQueue<byte[]> _changedRegions = new();
    private readonly IDelayer _delayer;
    private readonly AutoResetEventAsync _frameReadySignal = new();
    private readonly AutoResetEventAsync _frameRequestedSignal = new();
    private readonly ILogger<DisplayManager> _logger;
    private readonly IMemoryProvider _memoryProvider;
    private readonly IMessenger _messenger;
    private readonly Stopwatch _metricsBroadcastTimer = Stopwatch.StartNew();
    private readonly IScreenCapturer _screenCapturer;
    private readonly SemaphoreSlim _selectedDisplayLock = new(1, 1);
    private readonly ConcurrentQueue<SentFrame> _sentRegions = new();
    private readonly ISystemTime _systemTime;
    private readonly IWin32Interop _win32Interop;
    private volatile int _cpuFrames;
    private double _currentMbps;
    private int _currentQuality = _defaultImageQuality;
    private DisplayInfo[] _displays;
    private bool _forceKeyFrame = true;
    private long _frameCount;
    private volatile int _gpuFrames;
    private int _iterations;
    private Bitmap? _lastCpuBitmap;
    private string? _lastDisplayId;
    private Rectangle? _lastMonitorArea;
    private DisplayInfo? _selectedDisplay;
    private bool _needsKeyFrame = true;


    public DisplayManager(
        IMessenger messenger,
        IScreenCapturer screenCapturer,
        IBitmapUtility bitmapUtility,
        IMemoryProvider memoryProvider,
        IWin32Interop win32Interop,
        ISystemTime systemTime,
        IDelayer delayer,
        IHostApplicationLifetime appLifetime,
        ILogger<DisplayManager> logger)
    {
        _messenger = messenger;
        _screenCapturer = screenCapturer;
        _bitmapUtility = bitmapUtility;
        _memoryProvider = memoryProvider;
        _win32Interop = win32Interop;
        _systemTime = systemTime;
        _delayer = delayer;
        _appLifetime = appLifetime;
        _logger = logger;
        _displays = _screenCapturer.GetDisplays().ToArray();
        _selectedDisplay =
            _displays.FirstOrDefault(x => x.IsPrimary) ??
            _displays.FirstOrDefault();
    }

    public async Task ChangeDisplays(string displayId)
    {
        if (_displays.FirstOrDefault(x => x.DeviceName == displayId) is not { } newDisplay)
        {
            _logger.LogWarning("Could not find display with ID {DisplayId} when changing displays.", displayId);
            return;
        }

        await _selectedDisplayLock.WaitAsync();
        try
        {
            _lastCpuBitmap?.Dispose();
            _lastCpuBitmap = null;
            _selectedDisplay = newDisplay;
        }
        finally
        {
            _selectedDisplayLock.Release();
        }
    }

    public async Task<Point> ConvertPercentageLocationToAbsolute(double percentX, double percentY)
    {
        await _selectedDisplayLock.WaitAsync();
        try
        {
            if (_selectedDisplay is null)
            {
                return Point.Empty;
            }

            var bounds = _selectedDisplay.MonitorArea;
            var absoluteX = bounds.Width * percentX + bounds.Left;
            var absoluteY = bounds.Height * percentY + bounds.Top;
            return new Point((int)absoluteX, (int)absoluteY);
        }
        finally
        {
            _selectedDisplayLock.Release();
        }

    }

    public async IAsyncEnumerable<byte[]> GetChangedRegions()
    {
        try
        {
            while (_changedRegions.TryDequeue(out var region))
            {
                yield return region;
                await Task.Yield();
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
            .Select(x => new DisplayDto()
            {
                DisplayId = x.DeviceName,
                Height = x.MonitorArea.Height,
                IsPrimary = x.IsPrimary,
                Width = x.MonitorArea.Width,
                Name = x.DisplayName,
                Left = x.MonitorArea.Left,
                ScaleFactor = x.ScaleFactor
            });
    }

    public async void ResetDisplays()
    {
        _displays = _screenCapturer.GetDisplays().ToArray();
        await _selectedDisplayLock.WaitAsync();
        try
        {
            _selectedDisplay =
                _displays.FirstOrDefault(x => x.IsPrimary) ??
                _displays.FirstOrDefault();
            _lastCpuBitmap?.Dispose();
            _lastCpuBitmap = null;
            _forceKeyFrame = true;
        }
        finally
        {
            _selectedDisplayLock.Release();
        }
    }

    public Task StartCapturingChanges(ViewerReadyForStreamDto viewerReadyDto)
    {
        EncodeScreenCaptures(_appLifetime.ApplicationStopping).Forget();
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

        foreach (var region in captureResult.DirtyRects)
        {
            EncodeRegion(captureResult.Bitmap, region);
        }

        Interlocked.Increment(ref _gpuFrames);
    }

    private void EncodeRegion(Bitmap bitmap, Rectangle region, bool isKeyFrame = false)
    {
        Bitmap? cropped = null;

        try
        {
            var quality = isKeyFrame ? _defaultImageQuality : _currentQuality;

            using var ms = _memoryProvider.GetRecyclableStream();
            using var writer = new BinaryWriter(ms);

            cropped = _bitmapUtility.CropBitmap(bitmap, region);
            var imageData = _bitmapUtility.EncodeJpeg(cropped, quality);
            writer.Write(region.X);
            writer.Write(region.Y);
            writer.Write(region.Width);
            writer.Write(region.Height);
            writer.Write(imageData.Length);
            writer.Write(imageData);

            var regionData = ms.ToArray();
            _changedRegions.Enqueue(regionData);

            if (!isKeyFrame)
            {
                _sentRegions.Enqueue(new SentFrame(regionData.Length, _systemTime.Now));
            }
        }
        finally
        {
            cropped?.Dispose();
        }
    }

    private async Task EncodeScreenCaptures(CancellationToken stoppingToken)
    {
        _frameRequestedSignal.Set();

        while (!stoppingToken.IsCancellationRequested)
        {
            await _selectedDisplayLock.WaitAsync(stoppingToken);
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

                using var captureResult = _screenCapturer.Capture(_selectedDisplay, directXTimeout: 200);

                if (captureResult.DxTimedOut)
                {
                    _logger.LogDebug("DirectX capture timed out. BitBlt fallback used.");
                }

                if (!captureResult.IsSuccess)
                {
                    _logger.LogWarning(captureResult.Exception, "Failed to capture latest frame.  Reason: {ResultReason}", captureResult.FailureReason);
                    await _delayer.Delay(_afterFailureDelay, stoppingToken);
                    continue;
                }

                if (ShouldSendKeyFrame())
                {
                    EncodeRegion(captureResult.Bitmap, captureResult.Bitmap.ToRectangle(), isKeyFrame: true);
                    _forceKeyFrame = false;
                    _needsKeyFrame = false;
                    _lastCpuBitmap?.Dispose();
                    _lastCpuBitmap = null;
                    _lastDisplayId = _selectedDisplay?.DeviceName;
                    _lastMonitorArea = _selectedDisplay?.MonitorArea;
                    continue;
                }

                if (captureResult.IsUsingGpu)
                {
                    await EncodeGpuCaptureResult(captureResult);
                }
                else
                {
                    await EncodeCpuCaptureResult(captureResult, stoppingToken);
                }
                
                await ReportMetrics();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Screen frame pulls cancelled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while pulling frames.");
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
                _selectedDisplayLock.Release();
                await Task.Yield();
            }
        }
    }

    private int GetTargetImageQuality()
    {
        if (_currentMbps < _targetMbps)
        {
            return _defaultImageQuality;
        }

        var quality = (int)(_targetMbps / _currentMbps * _defaultImageQuality);
        var newQuality = Math.Max(quality, _minimumQuality);

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
        _needsKeyFrame = _needsKeyFrame || _currentQuality < _defaultImageQuality;

        // Keep only frames in our sample window.
        while (
            _sentRegions.TryPeek(out var frame) &&
            frame.Timestamp.AddSeconds(3) < _systemTime.Now)
        {
            _sentRegions.TryDequeue(out _);
        }

        if (_sentRegions.Count >= 2)
        {
            var sampleSpan = _sentRegions.Last().Timestamp - _sentRegions.First().Timestamp;
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

        return _needsKeyFrame && _currentMbps < _targetMbps * .5;
    }

    private async Task ThrottleStream()
    {
        if (_currentMbps > _targetMbps * 2)
        {
            var waitMs = Math.Min(100, _currentMbps / _targetMbps * 10);
            var waitTime = TimeSpan.FromMilliseconds(waitMs);
            await _delayer.Delay(waitTime);
        }
    }
    private record SentFrame(int Size, DateTimeOffset Timestamp);
}
