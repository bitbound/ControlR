using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using ControlR.Libraries.Shared.Dtos;

namespace ControlR.Agent.Services.Base;

internal abstract class AgentInstallerBase(
    IFileSystem _fileSystem,
    ISettingsProvider _settingsProvider,
    IOptionsMonitor<AgentAppOptions> _appOptions,
    ILogger<AgentInstallerBase> _logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    protected async Task UpdateAppSettings(Uri? serverUri, string? authorizedKey, string? label)
    {
        using var _ = _logger.BeginMemberScope();

        var appOptions = _appOptions.CurrentValue;

        var updatedServerUri =
            serverUri?.ToString().TrimEnd('/') ??
            appOptions.ServerUri?.TrimEnd('/') ??
            AppConstants.ServerUri;

        _logger.LogInformation("Setting server URI to {ServerUri}.", updatedServerUri);
        appOptions.ServerUri = updatedServerUri;

        var authorizedKeys = appOptions.AuthorizedKeys2 ?? [];

        var obsoleteKeys = appOptions.AuthorizedKeys
            .ExceptBy(authorizedKeys.Select(x => x.PublicKey), x => x)
            .Select(x => new AuthorizedKeyDto("", x));

        authorizedKeys.AddRange(obsoleteKeys);

        _logger.LogInformation("Updating authorized keys.  Initial count: {KeyCount}", authorizedKeys.Count);

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
                var newLabel = currentKey.Label ?? label ?? "";
                authorizedKeys[currentKeyIndex] = currentKey with { Label = newLabel };
            }
        }

        appOptions.AuthorizedKeys2 = authorizedKeys;

        if (string.IsNullOrWhiteSpace(appOptions.DeviceId))
        {
            _logger.LogInformation("DeviceId is empty.  Generating new one.");
            appOptions.DeviceId = RandomGenerator.CreateDeviceToken();
        }

        _logger.LogInformation("Writing results to disk.");
        var appSettings = new AgentAppSettings() { AppOptions = appOptions };
        var appSettingsJson = JsonSerializer.Serialize(appSettings, _jsonOptions);
        await _fileSystem.WriteAllTextAsync(_settingsProvider.GetAppSettingsPath(), appSettingsJson);
    }
}