using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Shared.Services.Linux;

public class DeviceInfoProviderLinux(
  IProcessManager processInvoker,
  IFileSystem fileSystem,
  ISystemEnvironment environmentHelper,
  ICpuUtilizationSampler cpuUtilizationSampler,
  IOptionsAccessor optionsAccessor,
  ILogger<DeviceInfoProviderLinux> logger)
  : DeviceInfoProviderBase(fileSystem, environmentHelper, cpuUtilizationSampler, optionsAccessor, logger), IDeviceInfoProvider
{
  private readonly ILogger<DeviceInfoProviderLinux> _logger = logger;
  private readonly IProcessManager _processInvoker = processInvoker;

  protected override async Task<string[]> GetCurrentUsers()
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

  protected override async Task<MemoryInfo> GetMemoryInGb()
  {
    try
    {
      var result = await _processInvoker.GetProcessOutput("cat", "/proc/meminfo");

      if (!result.IsSuccess)
      {
        _logger.LogResult(result);
        return new MemoryInfo(0, 0);
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

        return new MemoryInfo(totalGb - freeGb, totalGb);
      }

      return new MemoryInfo(0, 0);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting device memory.");
      return new MemoryInfo(0, 0);
    }
  }
}