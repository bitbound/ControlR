using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Streamer.Helpers;
using ControlR.Streamer.Messages;

namespace ControlR.Streamer.Services;

public interface ICaptureMetrics
{
  double Fps { get; }
  double Ips { get; }
  bool IsQualityReduced { get; }
  bool IsUsingGpu { get; }
  double Mbps { get; }
  int Quality { get; }

  Task BroadcastMetrics();
  void MarkBytesSent(int length);
  void MarkFrameSent();
  void MarkIteration();
  void SetIsUsingGpu(bool isUsingGpu);
  void Start(CancellationToken cancellationToken);
  void Stop();
  Task WaitForBandwidth(CancellationToken cancellationToken);
}

internal sealed class CaptureMetrics(
  TimeProvider timeProvider,
  IMessenger messenger,
  IWin32Interop win32Interop,
  ISystemEnvironment systemEnvironment,
  IScreenGrabber screenGrabber,
  IProcessManager processManager,
  ILogger<CaptureMetrics> logger) : ICaptureMetrics
{

  public const int DefaultImageQuality = 75;
  public const double MaxMbps = 8;
  public const int MinimumQuality = 20;
  public const double TargetMbps = 3;
  private readonly ManualResetEventAsync _bandwidthAvailableSignal = new(false);
  private readonly ConcurrentQueue<SentPayload> _bytesSent = [];
  private readonly ConcurrentQueue<DateTimeOffset> _framesSent = [];
  private readonly ConcurrentQueue<DateTimeOffset> _iterations = [];

  private readonly JsonSerializerOptions _jsonSerializerOptions = new()
  {
    WriteIndented = true
  };
  private readonly ILogger<CaptureMetrics> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly Stopwatch _metricsBroadcastTimer = Stopwatch.StartNew();
  private readonly TimeSpan _metricsWindow = TimeSpan.FromSeconds(1);
  private readonly SemaphoreSlim _processLock = new(1, 1);
  private readonly IProcessManager _processManager = processManager;
  private readonly IScreenGrabber _screenGrabber = screenGrabber;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly TimeSpan _timerInterval = TimeSpan.FromSeconds(.1);
  private readonly IWin32Interop _win32Interop = win32Interop;
  private CancellationTokenSource? _abortTokenSource;
  private double _fps;
  private double _ips;
  private bool _isUsingGpu;
  private double _mbps;
  private ITimer? _processingTimer;

  private int _quality = DefaultImageQuality;
  public double Fps => _fps;
  public double Ips => _ips;
  public bool IsQualityReduced => _quality < DefaultImageQuality;
  public bool IsUsingGpu => _isUsingGpu;
  public double Mbps => _mbps;
  public int Quality => _quality;

  public async Task BroadcastMetrics()
  {
    if (_metricsBroadcastTimer.Elapsed > _metricsWindow)
    {
      _metricsBroadcastTimer.Restart();
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
        extraData);

      var message = new CaptureMetricsChangedMessage(metricsDto);

      await _messenger.Send(message);
    }
  }

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
    _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    _processingTimer = _timeProvider.CreateTimer(ProcessMetrics, null, _timerInterval, _timerInterval);
  }

  public void Stop()
  {
    _abortTokenSource?.Cancel();
    _processingTimer?.Dispose();
    Disposer.TryDispose(_abortTokenSource);
    _abortTokenSource = null;
  }

  public async Task WaitForBandwidth(CancellationToken cancellationToken)
  {
    await _bandwidthAvailableSignal.Wait(cancellationToken);
  }
  private static double ConvertBytesToMbps(int bytes, TimeSpan timeSpan)
  {
    return bytes / 1024.0 / 1024.0 / timeSpan.TotalSeconds * 8;
  }
  private Dictionary<string, string> GetExtraData()
  {

    _ = _win32Interop.GetCurrentThreadDesktopName(out var threadDesktopName);
    _ = _win32Interop.GetInputDesktopName(out var inputDesktopName);
    var screenBounds = _screenGrabber.GetVirtualScreenBounds();

    var extraData = new Dictionary<string, string>
    {
      { "Thread ID", $"{_systemEnvironment.CurrentThreadId}" },
      { "Thread Desktop Name", $"{threadDesktopName}"},
      { "Input Desktop Name", $"{inputDesktopName}"},
      { "Screen Bounds", JsonSerializer.Serialize(screenBounds, _jsonSerializerOptions) }
    };

    return extraData;
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
          timestamp.Add(_metricsWindow) < _timeProvider.GetUtcNow())
      {
        _ = _framesSent.TryDequeue(out _);
      }

      if (_framesSent.Count >= 2)
      {
        _fps = _framesSent.Count / _metricsWindow.TotalSeconds;
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
        payload.Timestamp.Add(_metricsWindow) < _timeProvider.GetUtcNow())
      {
        _ = _bytesSent.TryDequeue(out _);
      }

      if (_bytesSent.Count >= 2)
      {
        var bytesSent = _bytesSent.Sum(x => x.Size);
        _mbps = ConvertBytesToMbps(_bytesSent.Sum(x => x.Size), _metricsWindow);
      }
      else if (_bytesSent.Count == 1)
      {
        _mbps = ConvertBytesToMbps(_bytesSent.First().Size, _metricsWindow);
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
          iteration.AddSeconds(1) < _timeProvider.GetUtcNow())
      {
        _ = _iterations.TryDequeue(out _);
      }

      _ips = _iterations.Count;

      var calculatedQuality = (int)(TargetMbps / _mbps * DefaultImageQuality);

      _quality = calculatedQuality < _quality ?
         Math.Max(calculatedQuality, MinimumQuality) :
         Math.Min(_quality + 2, DefaultImageQuality);
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Metrics processing cancelled.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while processing metrics.");
    }
    finally
    {
      _processLock.Release();
    }
  }

  private record SentPayload(int Size, DateTimeOffset Timestamp);
}
