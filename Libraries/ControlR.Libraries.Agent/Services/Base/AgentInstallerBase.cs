using System.Text.Json;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Agent.Services.Base;

internal abstract class AgentInstallerBase(
  IFileSystem fileSystem,
  ISettingsProvider settingsProvider,
  IControlrApi controlrApi,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerBase> logger)
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly ISettingsProvider _settingsProvider = settingsProvider;
  protected IOptionsMonitor<AgentAppOptions> AppOptions { get; } = appOptions;
  protected IControlrApi ControlrApi { get; } = controlrApi;
  protected IFileSystem FileSystem { get; } = fileSystem;
  protected ILogger<AgentInstallerBase> Logger { get; } = logger;

  protected async Task CreateDeviceOnServer(Uri? serverUri, Guid? tenantId, Guid[]? tags)
  {
    if (serverUri is null || tenantId is null || tags is null)
    {
      Logger.LogInformation("Required parameter for pre-creation is null.  Skipping.");
      return;
    }

    // TODO: Pre-create device on server.
    await Task.Yield();
  }

  protected async Task UpdateAppSettings(Uri? serverUri, Guid? tenantId)
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
    }


    Logger.LogInformation("Writing results to disk.");
    var appSettings = new AgentAppSettings { AppOptions = currentOptions };
    var appSettingsJson = JsonSerializer.Serialize(appSettings, _jsonOptions);
    await FileSystem.WriteAllTextAsync(_settingsProvider.GetAppSettingsPath(), appSettingsJson);
  }
}