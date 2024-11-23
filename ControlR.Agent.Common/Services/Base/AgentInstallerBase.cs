using System.Text.Json;
using ControlR.Libraries.Shared.Constants;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Base;

internal abstract class AgentInstallerBase(
  IFileSystem fileSystem,
  ISettingsProvider settingsProvider,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerBase> logger)
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly ISettingsProvider _settingsProvider = settingsProvider;
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
    }

    if (tagIds is { Length: > 0 })
    {
      Logger.LogInformation("Setting tag IDs to {TagIds}.", string.Join(", ", tagIds));
      currentOptions.TagIds = tagIds;
    }

    Logger.LogInformation("Writing results to disk.");
    var appSettings = new AgentAppSettings { AppOptions = currentOptions };
    var appSettingsJson = JsonSerializer.Serialize(appSettings, _jsonOptions);
    await FileSystem.WriteAllTextAsync(_settingsProvider.GetAppSettingsPath(), appSettingsJson);
  }
}