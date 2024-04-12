using ControlR.Agent.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ControlR.Agent.Services;

internal interface ICpuUtilizationSampler : IHostedService
{
    double CurrentUtilization { get; }
}

internal class CpuUtilizationSampler(IElevationChecker elevationChecker, ILogger<CpuUtilizationSampler> logger)
    : BackgroundService, ICpuUtilizationSampler
{
    private readonly HashSet<int> _ignoredProcesses = new();
    private double _currentUtilization;

    public double CurrentUtilization => _currentUtilization;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var currentUtil = await GetCpuUtilization(stoppingToken);
                Interlocked.Exchange(ref _currentUtilization, currentUtil);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Task canceled while taking CPU utilization sample.  Application may be shutting down.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while getting CPU utilization sample.");
            }
        }
    }

    private async Task<double> GetCpuUtilization(CancellationToken cancelToken)
    {
        double totalUtilization = 0;
        var utilizations = new Dictionary<int, Tuple<DateTimeOffset, TimeSpan>>();
        var processes = Process.GetProcesses();

        // If we're on Windows and not running in an elevated process,
        // don't try to get CPU utilization for session 0 processes. It
        // will throw.
        if (OperatingSystem.IsWindows() &&
            !elevationChecker.IsElevated())
        {
            processes = processes.Where(x => x.SessionId != 0).ToArray();
        }

        foreach (var proc in processes)
        {
            if (cancelToken.IsCancellationRequested)
            {
                return 0;
            }

            try
            {
                // Processes in other sessions (e.g. session 0) will be inaccessible
                // when debugging in a non-privileged process.  This prevents errors
                // from clogging up the output window.
                if (_ignoredProcesses.Contains(proc.Id))
                {
                    continue;
                }

                var startTime = DateTimeOffset.Now;
                var startCpuUsage = proc.TotalProcessorTime;
                utilizations.Add(proc.Id, new Tuple<DateTimeOffset, TimeSpan>(startTime, startCpuUsage));
            }
            catch
            {
                _ignoredProcesses.Add(proc.Id);
                continue;
            }
        }

        await Task.Delay(1_000, cancelToken);

        foreach (var kvp in utilizations)
        {
            if (cancelToken.IsCancellationRequested)
            {
                return 0;
            }

            var endTime = DateTimeOffset.Now;
            try
            {
                var proc = Process.GetProcessById(kvp.Key);
                var startTime = kvp.Value.Item1;
                var startCpuUsage = kvp.Value.Item2;
                var endCpuUsage = proc.TotalProcessorTime;
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                totalUtilization += cpuUsageTotal;
            }
            catch
            {
                continue;
            }
        }

        return totalUtilization;
    }
}