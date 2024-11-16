using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace ControlR.Libraries.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class CpuUtilizationSamplerWin(ILogger<CpuUtilizationSamplerWin> logger)
    : BackgroundService, ICpuUtilizationSampler
{
  private readonly ILogger<CpuUtilizationSamplerWin> _logger = logger;
  private PerformanceCounter? _cpuCounter;
  private double _currentUtilization;
  public double CurrentUtilization => _currentUtilization;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
      using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        GetNextSample();
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Aborting CPU sampling.  Application shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while executing CPU sampler.");
    }
    finally
    {
      _cpuCounter?.Dispose();
    }
  }

  private void GetNextSample()
  {
    try
    {
      _currentUtilization = (_cpuCounter?.NextValue() ?? 0) * .01;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting CPU utilization sample.");
    }
  }
}