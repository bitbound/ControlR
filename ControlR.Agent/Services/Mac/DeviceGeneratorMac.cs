﻿using ControlR.Agent.Interfaces;
using ControlR.Agent.Services.Base;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Services.Mac;

internal class DeviceDataGeneratorMac(
    IProcessManager processInvoker,
    IEnvironmentHelper environmentHelper,
    ILogger<DeviceDataGeneratorMac> logger) : DeviceDataGeneratorBase(environmentHelper, logger), IDeviceDataGenerator
{
    private readonly ILogger<DeviceDataGeneratorMac> _logger = logger;
    private readonly IProcessManager _processService = processInvoker;

    public async Task<DeviceDto> CreateDevice(double cpuUtilization, IEnumerable<AuthorizedKeyDto> authorizedKeys, string deviceId)
    {
        try
        {
            var (usedStorage, totalStorage) = GetSystemDriveInfo();
            var (usedMemory, totalMemory) = await GetMemoryInGB();

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
            _logger.LogError(ex, "Error getting device info.");
            throw;
        }
    }

    public async Task<(double usedGB, double totalGB)> GetMemoryInGB()
    {
        try
        {
            double totalGB = default;

            var memTotalResult = await _processService.GetProcessOutput("zsh", "-c \"sysctl -n hw.memsize\"");
            var memPercentResult = await _processService.GetProcessOutput("zsh", $"-c \"ps -A -o %mem\"");

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
                totalGB = (double)Math.Round(totalMemory / 1024 / 1024 / 1024, 2);
            }

            double usedGB = default;

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
            usedGB = usedMemPercent * totalGB;

            return (usedGB, totalGB);
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

    private async Task<string> GetCurrentUser()
    {
        var result = await _processService.GetProcessOutput("users", "");
        if (!result.IsSuccess)
        {
            _logger.LogResult(result);
            return string.Empty;
        }
        return result.Value?.Split()?.FirstOrDefault()?.Trim() ?? string.Empty;
    }
}