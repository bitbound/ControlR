using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Mac;

internal class DeviceDataGeneratorMac(
  IProcessManager processInvoker,
  ISystemEnvironment environmentHelper,
  ICpuUtilizationSampler cpuUtilizationSampler,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<DeviceDataGeneratorMac> logger)
  : DeviceDataGeneratorBase(environmentHelper, cpuUtilizationSampler, appOptions, logger), IDeviceDataGenerator
{
  private readonly ILogger<DeviceDataGeneratorMac> _logger = logger;
  private readonly IProcessManager _processService = processInvoker;

  public async Task<DeviceModel> CreateDevice(Guid deviceId)
  {
    try
    {
      var (usedStorage, totalStorage) = GetSystemDriveInfo();
      var (usedMemory, totalMemory) = await GetMemoryInGb();

      var currentUsers = await GetCurrentUser();
      var drives = GetAllDrives();
      var agentVersion = GetAgentVersion();

      return GetDeviceBase(
        deviceId,
        currentUsers,
        drives,
        usedStorage,
        totalStorage,
        usedMemory,
        totalMemory,
        agentVersion);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting device info.");
      throw;
    }
  }

  public async Task<(double usedGB, double totalGB)> GetMemoryInGb()
  {
    try
    {
      double totalGb = default;

      var memTotalResult = await _processService.GetProcessOutput("zsh", "-c \"sysctl -n hw.memsize\"");
      var memPercentResult = await _processService.GetProcessOutput("zsh", "-c \"ps -A -o %mem\"");

      if (!memTotalResult.IsSuccess)
      {
        _logger.LogResult(memTotalResult);
        return (0, 0);
      }

      if (!memPercentResult.IsSuccess)
      {
        _logger.LogResult(memPercentResult);
        return (0, 0);
      }

      if (double.TryParse(memTotalResult.Value, out var totalMemory))
      {
        totalGb = Math.Round(totalMemory / 1024 / 1024 / 1024, 2);
      }

      double usedGb = default;

      double usedMemPercent = 0;
      memPercentResult
        .Value
        .Split(Environment.NewLine)
        .ToList()
        .ForEach(x =>
        {
          if (double.TryParse(x, out var result))
          {
            usedMemPercent += result;
          }
        });

      usedMemPercent = usedMemPercent / 4 / 100;
      usedGb = usedMemPercent * totalGb;

      return (usedGb, totalGb);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting memory.");
      return (0, 0);
    }
  }

  private async Task<double> GetCpuUtilization()
  {
    try
    {
      var result = await _processService.GetProcessOutput("zsh", "-c \"ps -A -o %cpu\"");

      if (!result.IsSuccess)
      {
        _logger.LogResult(result);
        return 0;
      }

      double cpuPercent = 0;
      result
        .Value
        .Split(Environment.NewLine)
        .ToList()
        .ForEach(x =>
        {
          if (double.TryParse(x, out var result))
          {
            cpuPercent += result;
          }
        });

      return cpuPercent / Environment.ProcessorCount / 100;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting CPU utilization.");
    }

    return 0;
  }

  private async Task<string[]> GetCurrentUser()
  {
    var result = await _processService.GetProcessOutput("users", "");
    if (result.IsSuccess)
    {
      return [.. result.Value
        .Split()
        .Select(x => x.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct()];
    }

    _logger.LogResult(result);
    return [];
  }
}