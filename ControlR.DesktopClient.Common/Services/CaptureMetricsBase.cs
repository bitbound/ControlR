using System.Collections.Concurrent;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Common.Services;

public class CaptureMetricsBase(IServiceProvider serviceProvider) : BackgroundService, ICaptureMetrics
{

  public const int DefaultImageQuality = 75;
  public const double MaxMbps = 8;
  public const int MinimumQuality = 20;
  public const double TargetMbps = 3;
  protected readonly IHostApplicationLifetime _hostApplicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
  protected readonly ILogger<CaptureMetricsBase> _logger = serviceProvider.GetRequiredService<ILogger<CaptureMetricsBase>>();
  protected readonly IMessenger _messenger = serviceProvider.GetRequiredService<IMessenger>();
  protected readonly IProcessManager _processManager = serviceProvider.GetRequiredService<IProcessManager>();
  protected readonly IScreenGrabber _screenGrabber = serviceProvider.GetRequiredService<IScreenGrabber>();
  protected readonly ISystemEnvironment _systemEnvironment = serviceProvider.GetRequiredService<ISystemEnvironment>();

  private readonly ManualResetEventAsync _bandwidthAvailableSignal = new();
  private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(3);
  private readonly ConcurrentQueue<SentPayload> _bytesSent = [];
  private readonly ConcurrentQueue<DateTimeOffset> _framesSent = [];
  private readonly TimeProvider _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
  private double _fps;
  private bool _isUsingGpu;
  private double _mbps;
  private int _quality = DefaultImageQuality;

  public double Fps => _fps;
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

  public void SetIsUsingGpu(bool isUsingGpu)
  {
    _isUsingGpu = isUsingGpu;
  }

  public async Task WaitForBandwidth(CancellationToken cancellationToken)
  {
    await _bandwidthAvailableSignal.Wait(cancellationToken);
  }
  public override void Dispose()
  {
    base.Dispose();
    Disposer.DisposeAll(_bandwidthAvailableSignal);
    GC.SuppressFinalize(this);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(_broadcastInterval, _timeProvider);
    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        ProcessMetrics();
        await BroadcastMetrics();
      }
    }
    catch (OperationCanceledException)
    {
      // Expected when stopping
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error in CaptureMetricsBase ExecuteAsync.");
    }
  }

  protected virtual Dictionary<string, string> GetExtraData()
  {
    return [];
  }

  private static double ConvertBytesToMbps(int bytes, TimeSpan timeSpan)
  {
    return bytes / 1024.0 / 1024.0 / timeSpan.TotalSeconds * 8;
  }

  private async Task BroadcastMetrics()
  {
    try
    {
      _logger.LogDebug(
        "Mbps: {CurrentMbps:N2} | FPS: {Fps:N2} | Using GPU: {IsUsingGpu} | Current Quality: {ImageQuality}",
        Mbps,
        Fps,
        IsUsingGpu,
        Quality);

      var extraData = GetExtraData();

      var metricsDto = new CaptureMetricsDto(
        Mbps,
        Fps,
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
  private void ProcessMetrics()
  {
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
  }

  private record SentPayload(int Size, DateTimeOffset Timestamp);
}