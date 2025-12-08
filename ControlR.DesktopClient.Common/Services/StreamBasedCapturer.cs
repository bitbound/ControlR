using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Common.Services;

// Mock implementation. A real POC will be implemented later.
internal class StreamBasedCapturer(
    IScreenGrabber screenGrabber,
    IStreamEncoder encoder,
    IDisplayManager displayManager,
    ILogger<StreamBasedCapturer> logger) : IDesktopCapturer
{
    private readonly Channel<DtoWrapper> _channel = Channel.CreateBounded<DtoWrapper>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Lock _displayLock = new();
    private readonly IDisplayManager _displayManager = displayManager;
    private readonly IStreamEncoder _encoder = encoder;
    private readonly ILogger<StreamBasedCapturer> _logger = logger;
    private readonly IScreenGrabber _screenGrabber = screenGrabber;

    private Task? _captureTask;
    private bool _disposed;
    private volatile bool _forceKeyFrame;
    private DisplayInfo? _selectedDisplay;


  public async Task ChangeDisplays(string displayId)
    {
        var findResult = await _displayManager.TryFindDisplay(displayId);
        if (findResult.IsSuccess)
        {
            lock (_displayLock)
            {
                _selectedDisplay = findResult.Value;
            }
        }
        return;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _encoder.Dispose();
        if (_captureTask != null)
        {
            try
            {
                await _captureTask;
            }
            catch { }
        }
    }

    public async IAsyncEnumerable<DtoWrapper> GetCaptureStream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    public Task RequestKeyFrame()
    {
        _forceKeyFrame = true;
        return Task.CompletedTask;
    }

    public Task StartCapturingChanges(CancellationToken cancellationToken)
    {
        _captureTask = CaptureLoop(cancellationToken);
        return Task.CompletedTask;
    }

    public bool TryGetSelectedDisplay([NotNullWhen(true)] out DisplayInfo? display)
    {
        lock (_displayLock)
        {
            display = _selectedDisplay;
            return display != null;
        }
    }

    private async Task CaptureLoop(CancellationToken ct)
    {
        if (!TryGetSelectedDisplay(out var display))
        {
            var primary = await _displayManager.GetPrimaryDisplay();
            if (primary is null) return;
            lock (_displayLock)
            {
                _selectedDisplay = primary;
            }
            display = primary;
        }

        _encoder.Start(display.MonitorArea.Width, display.MonitorArea.Height, 75);

        // Producer: Capture and feed to encoder
        var producer = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var capture = await _screenGrabber.CaptureDisplay(display, captureCursor: true);
                    if (capture.IsSuccess)
                    {
                        _encoder.EncodeFrame(capture.Bitmap, _forceKeyFrame);
                        _forceKeyFrame = false;
                    }
                    else
                    {
                        await Task.Delay(100, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing frame.");
                }
            }
        }, ct);

        // Consumer: Read packets and push to channel
        while (!ct.IsCancellationRequested)
        {
            var packet = _encoder.GetNextPacket();
            if (packet != null)
            {
                var dto = new VideoStreamPacketDto(packet, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                var wrapper = DtoWrapper.Create(dto, DtoType.VideoStreamPacket);
                await _channel.Writer.WriteAsync(wrapper, ct);
            }
            else
            {
                await Task.Delay(1, ct);
            }
        }
        
        await producer;
    }
}
