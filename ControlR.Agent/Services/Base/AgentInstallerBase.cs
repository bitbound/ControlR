using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ControlR.Agent.Services.Base;

internal abstract class AgentInstallerBase(
    IFileSystem _fileSystem,
    ISettingsProvider _settingsProvider,
    IOptionsMonitor<AgentAppOptions> _appOptions,
    ILogger<AgentInstallerBase> _logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    protected async Task UpdateAppSettings(Uri? serverUri, string? authorizedKey)
    {
        using var _ = _logger.BeginMemberScope();

        var appOptions = _appOptions.CurrentValue;

        var updatedServerUri =
            serverUri?.ToString().TrimEnd('/') ??
            appOptions.ServerUri?.TrimEnd('/') ??
            AppConstants.ServerUri;

        _logger.LogInformation("Setting server URI to {ServerUri}.", updatedServerUri);
        appOptions.ServerUri = updatedServerUri;


        if (!string.IsNullOrWhiteSpace(authorizedKey) &&
            !appOptions.AuthorizedKeys.Contains(authorizedKey))
        {
            _logger.LogInformation("Adding key passed in from arguments.");
            appOptions.AuthorizedKeys.Add(authorizedKey);
            _logger.LogInformation("Key Count: {num}", appOptions.AuthorizedKeys.Count);
        }

        _logger.LogInformation("Removing duplicates.");
        appOptions.AuthorizedKeys.RemoveDuplicates();
        _logger.LogInformation("Key Count: {num}", appOptions.AuthorizedKeys.Count);

        _logger.LogInformation("Removing empties.");
        appOptions.AuthorizedKeys.RemoveAll(string.IsNullOrWhiteSpace);
        _logger.LogInformation("Key Count: {num}", appOptions.AuthorizedKeys.Count);

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