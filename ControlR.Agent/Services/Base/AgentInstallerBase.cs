using System.Text.Json;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services.Base;

internal abstract class AgentInstallerBase(
  IFileSystem _fileSystem,
  ISettingsProvider _settingsProvider,
  IOptionsMonitor<AgentAppOptions> _appOptions,
  ILogger<AgentInstallerBase> _logger)
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  protected IFileSystem FileSystem { get; } = _fileSystem;
  protected ISettingsProvider SettingsProvider { get; } = _settingsProvider;
  protected IOptionsMonitor<AgentAppOptions> AppOptions { get; } = _appOptions;
  protected ILogger<AgentInstallerBase> Logger { get; } = _logger;

  protected async Task UpdateAppSettings(Uri? serverUri)
  {
    using var _ = Logger.BeginMemberScope();

    var currentOptions = AppOptions.CurrentValue;

    var updatedServerUri =
      serverUri ??
      currentOptions.ServerUri ??
      AppConstants.ServerUri;

    Logger.LogInformation("Setting server URI to {ServerUri}.", updatedServerUri);
    currentOptions.ServerUri = updatedServerUri;
    
    if (currentOptions.DeviceId == Guid.Empty)
    {
      Logger.LogInformation("DeviceId is empty.  Generating new one.");
      currentOptions.DeviceId = Guid.NewGuid();
    }

    Logger.LogInformation("Writing results to disk.");
    var appSettings = new AgentAppSettings { AppOptions = currentOptions };
    var appSettingsJson = JsonSerializer.Serialize(appSettings, _jsonOptions);
    await FileSystem.WriteAllTextAsync(SettingsProvider.GetAppSettingsPath(), appSettingsJson);
  }

  protected async Task CreateDeviceOnServer(Uri? serverUri, Guid? deviceGroupId)
  {
    if (serverUri is null || deviceGroupId is null)
    {
      Logger.LogInformation("ServerUri or DeviceGroupId is null.  Skipping device pre-creation.");
      return;
    }
    // TODO: Add device pre-creation
    await Task.Yield();
  }
}