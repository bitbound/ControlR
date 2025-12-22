using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Common.Services;

// Mock implementation. A real POC will be implemented later.
internal class StreamBasedCapturer(
    IScreenGrabber screenGrabber,
    IStreamEncoder encoder,
    IDisplayManager displayManager,
    ILogger<StreamBasedCapturer> logger) : IDesktopCapturer
{
  private readonly Channel<DtoWrapper> _channel = Channel.CreateBounded<DtoWrapper>(
    new BoundedChannelOptions(capacity: 10)
    {
      SingleReader = true,
      SingleWriter = true,
      FullMode = BoundedChannelFullMode.Wait,
    });
  private readonly SemaphoreSlim _displayLock = new(1, 1);
  private readonly TimeSpan _displayLockTimeout = TimeSpan.FromSeconds(5);
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

  public async Task<Result<DisplayInfo>> TryGetSelectedDisplay()
  {
    using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
    if (_selectedDisplay is { } selected)
    {
      return Result.Ok(selected);
    }
    return Result.Fail<DisplayInfo>("No display selected.");
  }

  private async Task CaptureLoop(CancellationToken ct)
  {
    var selectResult = await TryGetSelectedDisplay();
    if (selectResult.IsSuccess)
    {
      _selectedDisplay = selectResult.Value;
    }
    else
    {
      var primary = await _displayManager.GetPrimaryDisplay();
      if (primary is null) return;

      if (primary is null)
      {
        throw new InvalidOperationException("Initial display could not be found.");
      }

      using var locker = await _displayLock.AcquireLockAsync(_displayLockTimeout);
      _selectedDisplay = primary;
    }

    _encoder.Start(_selectedDisplay.MonitorArea.Width, _selectedDisplay.MonitorArea.Height, 75);

    // Producer: Capture and feed to encoder
    var producer = Task.Run(async () =>
    {
      while (!ct.IsCancellationRequested)
      {
        try
        {
          using var capture = await _screenGrabber.CaptureDisplay(_selectedDisplay, captureCursor: true);
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
