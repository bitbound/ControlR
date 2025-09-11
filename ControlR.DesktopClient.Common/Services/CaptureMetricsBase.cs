using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ControlR.DesktopClient.Common.Services;

public class CaptureMetricsBase(
  TimeProvider timeProvider,
  IMessenger messenger,
  ISystemEnvironment systemEnvironment,
  IScreenGrabber screenGrabber,
  IProcessManager processManager,
  ILogger<CaptureMetricsBase> logger) : ICaptureMetrics
{

  public const int DefaultImageQuality = 75;
  public const double MaxMbps = 8;
  public const int MinimumQuality = 20;
  public const double TargetMbps = 3;
  protected readonly ILogger<CaptureMetricsBase> _logger = logger;
  protected readonly IMessenger _messenger = messenger;
  protected readonly IProcessManager _processManager = processManager;
  protected readonly IScreenGrabber _screenGrabber = screenGrabber;
  protected readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  private readonly ManualResetEventAsync _bandwidthAvailableSignal = new();
  private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(3);
  private readonly ConcurrentQueue<SentPayload> _bytesSent = [];
  private readonly ConcurrentQueue<DateTimeOffset> _framesSent = [];
  private readonly ConcurrentQueue<DateTimeOffset> _iterations = [];
  private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(.1);
  private readonly SemaphoreSlim _processLock = new(1, 1);
  private readonly TimeProvider _timeProvider = timeProvider;
  private CancellationTokenSource? _abortTokenSource;
  private ITimer? _broadcastTimer;
  private double _fps;
  private double _ips;
  private bool _isUsingGpu;
  private double _mbps;
  private ITimer? _processingTimer;
  private int _quality = DefaultImageQuality;
  private bool _disposedValue;


  public double Fps => _fps;
  public double Ips => _ips;
  public bool IsQualityReduced => _quality < DefaultImageQuality;
  public bool IsUsingGpu => _isUsingGpu;
  public double Mbps => _mbps;
  public int Quality => _quality;

  public void MarkBytesSent(int length)
  {
    _bytesSent.Enqueue(new SentPayload(length, _timeProvider.GetUtcNow()));
  }

  public void MarkFrameSent()
  {
    _framesSent.Enqueue(_timeProvider.GetUtcNow());
  }

  public void MarkIteration()
  {
    _iterations.Enqueue(_timeProvider.GetUtcNow());
  }

  public void SetIsUsingGpu(bool isUsingGpu)
  {
    _isUsingGpu = isUsingGpu;
  }

  public void Start(CancellationToken cancellationToken)
  {
    if (_disposedValue)
    {
      ObjectDisposedException.ThrowIf(_disposedValue, nameof(CaptureMetricsBase));
    }

    _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    _processingTimer = _timeProvider.CreateTimer(ProcessMetrics, null, _processingInterval, _processingInterval);
    _broadcastTimer = _timeProvider.CreateTimer(BroadcastMetrics, null, _broadcastInterval, _broadcastInterval);
  }

  public void Stop()
  {
    Dispose();
  }

  public async Task WaitForBandwidth(CancellationToken cancellationToken)
  {
    await _bandwidthAvailableSignal.Wait(cancellationToken);
  }

  protected virtual Dictionary<string, string> GetExtraData()
  {
    return [];
  }

  private static double ConvertBytesToMbps(int bytes, TimeSpan timeSpan)
  {
    return bytes / 1024.0 / 1024.0 / timeSpan.TotalSeconds * 8;
  }

  private async void BroadcastMetrics(object? state)
  {
    try
    {
      _logger.LogDebug(
        "Mbps: {CurrentMbps:N2} | FPS: {Fps:N2} | IPS (iterations): {IPS:N2} | Using GPU: {IsUsingGpu} | Current Quality: {ImageQuality}",
        Mbps,
        Fps,
        Ips,
        IsUsingGpu,
        Quality);

      var extraData = GetExtraData();

      var metricsDto = new CaptureMetricsDto(
        Mbps,
        Fps,
        Ips,
        IsUsingGpu,
        Quality,
        TimeSpan.Zero,
        extraData);

      var message = new CaptureMetricsChangedMessage(metricsDto);

      await _messenger.Send(message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while broadcasting metrics.");
    }
  }
  private void ProcessMetrics(object? state)
  {
    Guard.IsNotNull(_abortTokenSource, nameof(_abortTokenSource));

    if (_abortTokenSource.IsCancellationRequested)
    {
      return;
    }

    if (!_processLock.Wait(0, _abortTokenSource.Token))
    {
      return;
    }

    try
    {

      while (
          _framesSent.TryPeek(out var timestamp) &&
          timestamp.Add(_broadcastInterval) < _timeProvider.GetUtcNow())
      {
        _ = _framesSent.TryDequeue(out _);
      }

      if (_framesSent.Count >= 2)
      {
        _fps = _framesSent.Count / _broadcastInterval.TotalSeconds;
      }
      else if (_framesSent.Count == 1)
      {
        _fps = 1;
      }
      else
      {
        _fps = 0;
      }

      while (
        _bytesSent.TryPeek(out var payload) &&
        payload.Timestamp.Add(_broadcastInterval) < _timeProvider.GetUtcNow())
      {
        _ = _bytesSent.TryDequeue(out _);
      }

      if (_bytesSent.Count >= 2)
      {
        _mbps = ConvertBytesToMbps(_bytesSent.Sum(x => x.Size), _broadcastInterval);
      }
      else if (_bytesSent.Count == 1)
      {
        _mbps = ConvertBytesToMbps(_bytesSent.First().Size, _broadcastInterval);
      }
      else
      {
        _mbps = 0;
      }

      if (_mbps >= MaxMbps && _bandwidthAvailableSignal.IsSet)
      {
        _bandwidthAvailableSignal.Reset();
      }
      else if (_mbps < MaxMbps && !_bandwidthAvailableSignal.IsSet)
      {
        _bandwidthAvailableSignal.Set();
      }

      while (
          _iterations.TryPeek(out var iteration) &&
          iteration.Add(_broadcastInterval) < _timeProvider.GetUtcNow())
      {
        _ = _iterations.TryDequeue(out _);
      }


      if (_iterations.Count >= 2)
      {
        _ips = _iterations.Count / _broadcastInterval.TotalSeconds;
      }
      else if (_iterations.Count == 1)
      {
        _ips = 1;
      }
      else
      {
        _ips = 0;
      }

      var calculatedQuality = _mbps > 0 ? 
         (int)(TargetMbps / _mbps * DefaultImageQuality) : 
         DefaultImageQuality;

      _quality = calculatedQuality < _quality ?
         Math.Max(calculatedQuality, MinimumQuality) :
         Math.Min(_quality + 2, DefaultImageQuality);
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Metrics processing cancelled.");
    }
    catch (ObjectDisposedException ex)
    {
      _logger.LogInformation(ex, "Metrics processing cancelled.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error while processing metrics.");
    }
    finally
    {
      try
      {
        _processLock.Release();
      }
      catch (ObjectDisposedException)
      {
        // This can happen if service is getting disposed.
      }
    }
  }

  private record SentPayload(int Size, DateTimeOffset Timestamp);

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        Disposer.DisposeAll(
          _broadcastTimer,
          _processingTimer,
          _abortTokenSource,
          _bandwidthAvailableSignal,
          _processLock);
      }
      _disposedValue = true;
    }
  }
  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}