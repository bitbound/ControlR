using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Shared.Services.Mac;

public class DeviceInfoProviderMac(
  IProcessManager processManager,
  IFileSystem fileSystem,
  ISystemEnvironment environmentHelper,
  ICpuUtilizationSampler cpuUtilizationSampler,
  IOptionsAccessor optionsAccessor,
  ILogger<DeviceInfoProviderMac> logger)
  : DeviceInfoProviderBase(fileSystem, environmentHelper, cpuUtilizationSampler, optionsAccessor, logger), IDeviceInfoProvider
{
  private readonly ILogger<DeviceInfoProviderMac> _logger = logger;
  private readonly IProcessManager _processManager = processManager;

  protected override async Task<string[]> GetCurrentUsers()
  {
    var result = await _processManager.GetProcessOutput("users", "");
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

  protected override async Task<MemoryInfo> GetMemoryInGb()
  {
    try
    {
      double totalGb = default;

      var memTotalResult = await _processManager.GetProcessOutput("zsh", "-c \"sysctl -n hw.memsize\"");
      var memPercentResult = await _processManager.GetProcessOutput("zsh", "-c \"ps -A -o %mem\"");

      if (!memTotalResult.IsSuccess)
      {
        _logger.LogResult(memTotalResult);
        return new MemoryInfo(0, 0);
      }

      if (!memPercentResult.IsSuccess)
      {
        _logger.LogResult(memPercentResult);
        return new MemoryInfo(0, 0);
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

      return new MemoryInfo(usedGb, totalGb);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting memory.");
      return new MemoryInfo(0, 0);
    }
  }
}