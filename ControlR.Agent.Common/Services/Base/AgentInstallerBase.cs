using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Base;

internal abstract class AgentInstallerBase(
  IFileSystem fileSystem,
  IControlrApi controlrApi,
  IDeviceInfoProvider deviceDataGenerator,
  ISettingsProvider settingsProvider,
  IProcessManager processManager,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerBase> logger)
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDeviceInfoProvider _deviceDataGenerator = deviceDataGenerator;
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

    tagIds ??= [];

    var deviceDto = await _deviceDataGenerator.CreateDevice();

    Logger.LogInformation("Requesting device creation on the server with tags {TagIds}.", string.Join(", ", tagIds));
    var createResult = await _controlrApi.CreateDevice(deviceDto, installerKeyId.Value, installerKeySecret, tagIds);
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

  /// <summary>
  /// Attempts to clear old .NET extraction directories to free up space.
  /// </summary>
  /// <param name="agentTempDirBase">
  ///   The base directory where .NET extracts files for the agent (e.g. "C:\Windows\SystemTemp\.net\ControlR.Agent").
  /// </param>
  protected void TryClearDotnetExtractDir(string agentTempDirBase)
  {
    try
    {
      if (!FileSystem.DirectoryExists(agentTempDirBase))
      {
        return;
      }

      var subdirs = FileSystem
        .GetDirectories(agentTempDirBase)
        .Select(x => new DirectoryInfo(x))
        .OrderByDescending(x => x.CreationTime)
        .Skip(3)
        .ToArray();

      foreach (var subdir in subdirs)
      {
        try
        {
          subdir.Delete(true);
        }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Failed to delete directory {SubDir}.", subdir);
        }
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while cleaning up .net extraction directory.");
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