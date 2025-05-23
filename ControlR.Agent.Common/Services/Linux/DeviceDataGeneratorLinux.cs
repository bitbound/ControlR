﻿using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Services.Base;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Linux;

internal class DeviceDataGeneratorLinux(
  IProcessManager processInvoker,
  ISystemEnvironment environmentHelper,
  ICpuUtilizationSampler cpuUtilizationSampler,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<DeviceDataGeneratorLinux> logger)
  : DeviceDataGeneratorBase(environmentHelper, cpuUtilizationSampler, appOptions, logger), IDeviceDataGenerator
{
  private readonly ILogger<DeviceDataGeneratorLinux> _logger = logger;
  private readonly IProcessManager _processInvoker = processInvoker;

  public async Task<DeviceModel> CreateDevice(Guid deviceId)
  {
    try
    {
      var (usedStorage, totalStorage) = GetSystemDriveInfo();
      var (usedMemory, totalMemory) = await GetMemoryInGb();

      var currentUsers = await GetCurrentUsers();
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
      _logger.LogError(ex, "Error getting device data.");
      throw;
    }
  }

  public async Task<(double usedGB, double totalGB)> GetMemoryInGb()
  {
    try
    {
      var result = await _processInvoker.GetProcessOutput("cat", "/proc/meminfo");

      if (!result.IsSuccess)
      {
        _logger.LogResult(result);
        return (0, 0);
      }

      var resultsArr = result.Value.Split("\n".ToCharArray());
      var freeKb = resultsArr
        .FirstOrDefault(x => x.Trim().StartsWith("MemAvailable"))?
        .Trim()
        .Split(" ".ToCharArray(), 2)
        .Last() // 9168236 kB
        .Trim()
        .Split(' ')
        .First(); // 9168236

      var totalKb = resultsArr
        .FirstOrDefault(x => x.Trim().StartsWith("MemTotal"))?
        .Trim()
        .Split(" ".ToCharArray(), 2)
        .Last() // 16637468 kB
        .Trim()
        .Split(' ')
        .First(); // 16637468

      if (double.TryParse(freeKb, out var freeKbDouble) &&
          double.TryParse(totalKb, out var totalKbDouble))
      {
        var freeGb = Math.Round(freeKbDouble / 1024 / 1024, 2);
        var totalGb = Math.Round(totalKbDouble / 1024 / 1024, 2);

        return (totalGb - freeGb, totalGb);
      }

      return (0, 0);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting device memory.");
      return (0, 0);
    }
  }

  private async Task<string[]> GetCurrentUsers()
  {
    var result = await _processInvoker.GetProcessOutput("users", "");
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