using System.Text.Json;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models;
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
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerBase> logger)
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly ISettingsProvider _settingsProvider = settingsProvider;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDeviceDataGenerator _deviceDataGenerator = deviceDataGenerator;
  protected IOptionsMonitor<AgentAppOptions> AppOptions { get; } = appOptions;
  protected IFileSystem FileSystem { get; } = fileSystem;
  protected ILogger<AgentInstallerBase> Logger { get; } = logger;

  protected async Task UpdateAppSettings(Uri? serverUri, Guid? tenantId, Guid[]? tagIds)
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

    Logger.LogInformation("Setting server URI to {ServerUri}.", updatedServerUri);
    currentOptions.ServerUri = updatedServerUri;

    Logger.LogInformation("Setting tenant ID to {TenantId}.", updatedTenantId);
    currentOptions.TenantId = updatedTenantId;

    if (currentOptions.DeviceId == Guid.Empty)
    {
      Logger.LogInformation("DeviceId is empty.  Generating new one.");
      currentOptions.DeviceId = Guid.NewGuid();

      if (tagIds is { Length: > 0 } tags)
      {
        var device = await _deviceDataGenerator.CreateDevice(currentOptions.DeviceId);
        device.TenantId = updatedTenantId;
        device.TagIds = tags;
        var dto = device.CloneAs<DeviceModel, DeviceDto>();

        Logger.LogInformation("Requesting device creation on the server with tags {TagIds}.", string.Join(", ", tagIds));
        var result = await _controlrApi.CreateDevice(dto);
        if (result.IsSuccess)
        {
          Logger.LogInformation("Device created successfully.");
        }
        else
        {
          Logger.LogError(result.Exception, "Device creation failed.  Reason: {Reason}", result.Reason);
        }
      }
    }

    Logger.LogInformation("Writing results to disk.");
    var appSettings = new AgentAppSettings { AppOptions = currentOptions };
    var appSettingsJson = JsonSerializer.Serialize(appSettings, _jsonOptions);
    await FileSystem.WriteAllTextAsync(_settingsProvider.GetAppSettingsPath(), appSettingsJson);
  }
}