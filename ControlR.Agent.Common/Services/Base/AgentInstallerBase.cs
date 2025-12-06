using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Base;

internal abstract class AgentInstallerBase(
  IFileSystem fileSystem,
  IControlrApi controlrApi,
  IDeviceDataGenerator deviceDataGenerator,
  ISettingsProvider settingsProvider,
  IProcessManager processManager,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerBase> logger)
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDeviceDataGenerator _deviceDataGenerator = deviceDataGenerator;
  private readonly ISettingsProvider _settingsProvider = settingsProvider;
  protected IOptionsMonitor<AgentAppOptions> AppOptions { get; } = appOptions;
  protected IFileSystem FileSystem { get; } = fileSystem;
  protected ILogger<AgentInstallerBase> Logger { get; } = logger;
  protected IProcessManager ProcessManager { get; } = processManager;

  protected async Task<Result> CreateDeviceOnServer(Guid? installerKeyId, string? installerKeySecret, Guid[]? tagIds)
  {
    if (installerKeyId is null)
    {
      return Result.Ok();
    }

    if (string.IsNullOrWhiteSpace(installerKeySecret))
    {
      return Result.Fail("Installer key secret is required when installer key ID is provided.");
    }

    var currentOptions = AppOptions.CurrentValue;
    tagIds ??= [];

    var device = await _deviceDataGenerator.CreateDevice(currentOptions.DeviceId);
    device.TenantId = currentOptions.TenantId;
    device.TagIds = tagIds;
    var deviceDto = device.CloneAs<DeviceModel, DeviceDto>();

    Logger.LogInformation("Requesting device creation on the server with tags {TagIds}.", string.Join(", ", tagIds));
    var createResult = await _controlrApi.CreateDevice(deviceDto, installerKeyId.Value, installerKeySecret);
    if (createResult.IsSuccess)
    {
      Logger.LogInformation("Device created successfully.");
    }
    else
    {
      Logger.LogError(createResult.Exception, "Device creation failed.  Reason: {Reason}", createResult.Reason);
    }

    return createResult;
  }

  protected Result StopProcesses()
  {
    try
    {
      var procs = ProcessManager
        .GetProcessesByName("ControlR.Agent")
        .Where(x => x.Id != Environment.ProcessId);

      foreach (var proc in procs)
      {
        try
        {
          proc.Kill();
        }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Failed to kill agent process with ID {AgentProcessId}.", proc.Id);
        }
      }

      procs = ProcessManager.GetProcessesByName("ControlR.DesktopClient");

      foreach (var proc in procs)
      {
        try
        {
          proc.Kill();
        }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Failed to kill desktop client process with ID {DesktopClientProcessId}.", proc.Id);
        }
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while stopping service and processes.");
      return Result.Fail(ex);
    }
  }

  protected async Task UpdateAppSettings(Uri? serverUri, Guid? tenantId, Guid? deviceId)
  {
    using var _ = Logger.BeginMemberScope();
    var currentOptions = AppOptions.CurrentValue;

    var updatedServerUri =
      serverUri ??
      currentOptions.ServerUri ??
      AppConstants.ServerUri;

    var updatedTenantId =
      tenantId ??
      currentOptions.TenantId;

    var updatedDeviceId =
      deviceId ??
      currentOptions.DeviceId;

    Logger.LogInformation("Setting server URI to {ServerUri}.", updatedServerUri);
    currentOptions.ServerUri = updatedServerUri;

    Logger.LogInformation("Setting tenant ID to {TenantId}.", updatedTenantId);
    currentOptions.TenantId = updatedTenantId;

    if (updatedDeviceId == Guid.Empty)
    {
      Logger.LogInformation("DeviceId is empty.  Generating new one.");
      currentOptions.DeviceId = Guid.NewGuid();
    }
    else
    {
      Logger.LogInformation("Setting device ID to {DeviceId}.", updatedDeviceId);
      currentOptions.DeviceId = updatedDeviceId;
    }

    Logger.LogInformation("Writing results to disk.");
    await _settingsProvider.UpdateAppOptions(currentOptions);
  }
}