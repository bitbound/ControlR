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
  public const double MaxMbps = 10;
  protected readonly ILogger<CaptureMetricsBase> _logger = serviceProvider.GetRequiredService<ILogger<CaptureMetricsBase>>();
  protected readonly IMessenger _messenger = serviceProvider.GetRequiredService<IMessenger>();
  protected readonly IProcessManager _processManager = serviceProvider.GetRequiredService<IProcessManager>();
  protected readonly ISystemEnvironment _systemEnvironment = serviceProvider.GetRequiredService<ISystemEnvironment>();

  private readonly ManualResetEventAsync _bandwidthAvailableSignal = new();
  private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(3);
  private readonly ConcurrentQueue<SentPayload> _bytesSent = [];
  private readonly ConcurrentQueue<DateTimeOffset> _framesSent = [];
  private readonly TimeSpan _processInterval = TimeSpan.FromSeconds(.1);
  private readonly TimeProvider _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
  private double _fps;
  private bool _isUsingGpu;
  private double _mbps;

  public double Fps => _fps;

  public bool IsUsingGpu => _isUsingGpu;
  public double Mbps => _mbps;
  public override void Dispose()
  {
    base.Dispose();
    Disposer.DisposeAll(_bandwidthAvailableSignal);
    GC.SuppressFinalize(this);
  }

  public void MarkBytesSent(int length)
  {
    lock (_bytesSent)
    {
      _bytesSent.Enqueue(new SentPayload(length, _timeProvider.GetUtcNow()));
    }
  }

  public void MarkFrameSent()
  {
    lock (_framesSent)
    {
      _framesSent.Enqueue(_timeProvider.GetUtcNow());
    }
  }

  public void SetIsUsingGpu(bool isUsingGpu)
  {
    _isUsingGpu = isUsingGpu;
  }

  public async Task WaitForBandwidth(CancellationToken cancellationToken)
  {
    await _bandwidthAvailableSignal.Wait(cancellationToken);
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
        cts.Cancel();
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
    using var timer = new PeriodicTimerEx(_broadcastInterval, _timeProvider);
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
  }

  private SentPayload[] GetBytesSent()
  {
    lock (_bytesSent)
    {
      while (
        _bytesSent.TryPeek(out var payload) &&
        payload.Timestamp.Add(_broadcastInterval) < _timeProvider.GetUtcNow())
      {
        _ = _bytesSent.TryDequeue(out _);
      }
      return [.. _bytesSent];
    }
  }
  private DateTimeOffset[] GetFramesSent()
  {
    lock (_framesSent)
    {
      while (
        _framesSent.TryPeek(out var timestamp) &&
        timestamp.Add(_broadcastInterval) < _timeProvider.GetUtcNow())
      {
        _ = _framesSent.TryDequeue(out _);
      }
      return [.. _framesSent];
    }
  }
  private async Task ProcessMetrics(CancellationToken cancellationToken)
  {
    using var timer = new PeriodicTimerEx(_processInterval, _timeProvider);
    while (await timer.WaitForNextTick(throwOnCancellation: false, cancellationToken))
    {
      try
      {
        var framesSent = GetFramesSent();
        var bytesSent = GetBytesSent();

        if (framesSent.Length >= 2)
        {
          _fps = framesSent.Length / _broadcastInterval.TotalSeconds;
        }
        else if (framesSent.Length == 1)
        {
          _fps = 1;
        }
        else
        {
          _fps = 0;
        }

        if (bytesSent.Length >= 2)
        {
          _mbps = ConvertBytesToMbps(bytesSent.Sum(x => x.Size), _broadcastInterval);
        }
        else if (bytesSent.Length == 1)
        {
          _mbps = ConvertBytesToMbps(bytesSent.First().Size, _broadcastInterval);
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