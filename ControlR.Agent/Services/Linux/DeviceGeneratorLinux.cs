﻿using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using ControlR.Libraries.Shared.Extensions;

namespace ControlR.Agent.Services.Linux;

internal class DeviceDataGeneratorLinux(
  IProcessManager processInvoker,
  IEnvironmentHelper environmentHelper,
  ILogger<DeviceDataGeneratorLinux> logger) : DeviceDataGeneratorBase(environmentHelper, logger), IDeviceDataGenerator
{
  private readonly ILogger<DeviceDataGeneratorLinux> _logger = logger;
  private readonly IProcessManager _processInvoker = processInvoker;

  public async Task<DeviceDto> CreateDevice(double cpuUtilization, IEnumerable<AuthorizedKeyDto> authorizedKeys,
    string deviceId)
  {
    try
    {
      var (usedStorage, totalStorage) = GetSystemDriveInfo();
      var (usedMemory, totalMemory) = await GetMemoryInGb();

      var currentUser = await GetCurrentUser();
      var drives = GetAllDrives();
      var agentVersion = GetAgentVersion();

      return GetDeviceBase(
        authorizedKeys,
        deviceId,
        currentUser,
        drives,
        usedStorage,
        totalStorage,
        usedMemory,
        totalMemory,
        cpuUtilization,
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

  private async Task<string> GetCurrentUser()
  {
    var result = await _processInvoker.GetProcessOutput("users", "");
    if (!result.IsSuccess)
    {
      return string.Empty;
    }

    return result.Value.Split()?.FirstOrDefault()?.Trim() ?? string.Empty;
  }
}