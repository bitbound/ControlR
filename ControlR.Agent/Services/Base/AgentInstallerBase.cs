using System.Text.Json;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services.Base;

internal abstract class AgentInstallerBase(
  IFileSystem fileSystem,
  ISettingsProvider settingsProvider,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerBase> logger)
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  protected async Task UpdateAppSettings(Uri? serverUri, string? authorizedKey, string? label)
  {
    using var _ = logger.BeginMemberScope();

    var currentOptions = appOptions.CurrentValue;

    var updatedServerUri =
      serverUri ??
      currentOptions.ServerUri ??
      AppConstants.ServerUri;

    logger.LogInformation("Setting server URI to {ServerUri}.", updatedServerUri);
    currentOptions.ServerUri = updatedServerUri;

    var authorizedKeys = currentOptions.AuthorizedKeys ?? [];

    logger.LogInformation("Updating authorized keys.  Initial count: {KeyCount}", authorizedKeys.Count);

    if (!string.IsNullOrWhiteSpace(authorizedKey))
    {
      var currentKeyIndex = authorizedKeys.FindIndex(x => x.PublicKey == authorizedKey);
      if (currentKeyIndex == -1)
      {
        authorizedKeys.Add(new AuthorizedKeyDto(label ?? "", authorizedKey));
      }
      else
      {
        var currentKey = authorizedKeys[currentKeyIndex];
        var newLabel = label ?? currentKey.Label ?? "";
        authorizedKeys[currentKeyIndex] = currentKey with { Label = newLabel };
      }
    }

    currentOptions.AuthorizedKeys = authorizedKeys;

    if (string.IsNullOrWhiteSpace(currentOptions.DeviceId))
    {
      logger.LogInformation("DeviceId is empty.  Generating new one.");
      currentOptions.DeviceId = RandomGenerator.CreateDeviceToken();
    }

    logger.LogInformation("Writing results to disk.");
    var appSettings = new AgentAppSettings { AppOptions = currentOptions };
    var appSettingsJson = JsonSerializer.Serialize(appSettings, _jsonOptions);
    await fileSystem.WriteAllTextAsync(settingsProvider.GetAppSettingsPath(), appSettingsJson);
  }
}