using System.Collections.Concurrent;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Common.Services;

public class CaptureMetricsBase(IServiceProvider serviceProvider) : BackgroundService, ICaptureMetrics
{
  protected readonly ISystemEnvironment SystemEnvironment = serviceProvider.GetRequiredService<ISystemEnvironment>();

  private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(3);
  private readonly ConcurrentQueue<SentPayload> _bytesSent = [];
  private readonly ConcurrentQueue<DateTimeOffset> _framesSent = [];

  private readonly ILogger<CaptureMetricsBase> _logger =
    serviceProvider.GetRequiredService<ILogger<CaptureMetricsBase>>();

  private readonly IMessenger _messenger = serviceProvider.GetRequiredService<IMessenger>();
  private readonly TimeSpan _processInterval = TimeSpan.FromSeconds(.1);
  private readonly TimeProvider _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

  public double Fps { get; private set; }

  public bool IsUsingGpu { get; private set; }

  public double Mbps { get; private set; }

  public override void Dispose()
  {
    base.Dispose();
    GC.SuppressFinalize(this);
  }

  public void MarkBytesSent(int length)
  {
    using var acquiredLock = _bytesSent.Lock();
    _bytesSent.Enqueue(new SentPayload(length, _timeProvider.GetUtcNow()));
  }

  public void MarkFrameSent()
  {
    using var acquiredLock = _framesSent.Lock();
    _framesSent.Enqueue(_timeProvider.GetUtcNow());
  }

  public void SetIsUsingGpu(bool isUsingGpu)
  {
    IsUsingGpu = isUsingGpu;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var processTask = ProcessMetrics(cts.Token);
        var broadcastTask = BroadcastMetrics(cts.Token);
        await Task.WhenAll(processTask, broadcastTask);
        await cts.CancelAsync();
      }
      catch (OperationCanceledException)
      {
        // Expected during shutdown.
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error in capture metrics service.");
      }
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

  private async Task BroadcastMetrics(CancellationToken cancellationToken)
  {
    using var timer = new PeriodicTimer(_broadcastInterval, _timeProvider);
    while (await timer.WaitForNextTick(throwOnCancellation: false, cancellationToken))
    {
      try
      {
        _logger.LogDebug(
          "Mbps: {CurrentMbps:N2} | FPS: {Fps:N2} | Using GPU: {IsUsingGpu}",
          Mbps,
          Fps,
          IsUsingGpu);

        var extraData = GetExtraData();

        var metricsDto = new CaptureMetricsDto(
          Mbps,
          Fps,
          IsUsingGpu,
          _timeProvider.GetUtcNow(),
          extraData);

        var message = new CaptureMetricsChangedMessage(metricsDto);

        await _messenger.Send(message);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while broadcasting metrics.");
      }
    }
  }

  private SentPayload[] GetBytesSent()
  {
    using var acquiredLock = _bytesSent.Lock();
    
    while (
      _bytesSent.TryPeek(out var payload) &&
      payload.Timestamp.Add(_broadcastInterval) < _timeProvider.GetUtcNow())
    {
      _ = _bytesSent.TryDequeue(out _);
    }

    return [.. _bytesSent];
  }

  private DateTimeOffset[] GetFramesSent()
  {
    using var acquiredLock = _framesSent.Lock();
    while (
      _framesSent.TryPeek(out var timestamp) &&
      timestamp.Add(_broadcastInterval) < _timeProvider.GetUtcNow())
    {
      _ = _framesSent.TryDequeue(out _);
    }

    return [.. _framesSent];
  }

  private async Task ProcessMetrics(CancellationToken cancellationToken)
  {
    using var timer = new PeriodicTimer(_processInterval, _timeProvider);
    while (await timer.WaitForNextTick(throwOnCancellation: false, cancellationToken))
    {
      try
      {
        var framesSent = GetFramesSent();
        var bytesSent = GetBytesSent();

        Fps = framesSent.Length switch
        {
          >= 2 => framesSent.Length / _broadcastInterval.TotalSeconds,
          1 => 1,
          _ => 0
        };

        Mbps = bytesSent.Length switch
        {
          >= 2 => ConvertBytesToMbps(bytesSent.Sum(x => x.Size), _broadcastInterval),
          1 => ConvertBytesToMbps(bytesSent.First().Size, _broadcastInterval),
          _ => 0
        };
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
  }


  private record SentPayload(int Size, DateTimeOffset Timestamp);
}