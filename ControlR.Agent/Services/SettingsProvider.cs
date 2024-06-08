using Bitbound.SimpleMessenger;
using ControlR.Agent.Options;
using ControlR.Agent.Startup;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ControlR.Agent.Services;

internal interface ISettingsProvider
{
    IReadOnlyList<string> AuthorizedKeys { get; }
    string DeviceId { get; }
    Uri ServerUri { get; }

    string GetAppSettingsPath();

    Task UpdateSettings(AgentAppSettings settings);
}

internal class SettingsProvider(
    IOptionsMonitor<AgentAppOptions> _appOptions,
    IMessenger _messenger,
    IDelayer _delayer,
    IFileSystem _fileSystem,
    IOptions<InstanceOptions> _instanceOptions,
    ILogger<SettingsProvider> _logger) : ISettingsProvider
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };


    public IReadOnlyList<string> AuthorizedKeys
    {
        get => _appOptions.CurrentValue.AuthorizedKeys ?? [];
    }

    public string DeviceId
    {
        get => _appOptions.CurrentValue.DeviceId;
    }

    public Uri ServerUri
    {
        get
        {
            if (Uri.TryCreate(_appOptions.CurrentValue.ServerUri, UriKind.Absolute, out var serverUri))
            {
                return serverUri;
            }

            if (Uri.TryCreate(AppConstants.ServerUri, UriKind.Absolute, out serverUri))
            {
                return serverUri;
            }

            throw new InvalidOperationException("Server URI is not configured correctly.");
        }
    }

    public string GetAppSettingsPath()
    {
        return PathConstants.GetAppSettingsPath(_instanceOptions.Value.InstanceId);
    }

    public async Task UpdateSettings(AgentAppSettings settings)
    {
        try
        {
            if (!Uri.TryCreate(settings.AppOptions.ServerUri, UriKind.Absolute, out var newServerUri))
            {
                _logger.LogWarning("ServerUri was invalid while attempting to update app settings.");
                return;
            }

            var serverUriChanged = newServerUri != ServerUri;

            var content = JsonSerializer.Serialize(settings, _jsonOptions);
            await _fileSystem.WriteAllTextAsync(GetAppSettingsPath(), content);

            if (serverUriChanged)
            {
                var waitResult = await _delayer.WaitForAsync(
                    () => ServerUri == newServerUri,
                    TimeSpan.FromSeconds(5));

                if (!waitResult)
                {
                    _logger.LogError(
                        "ServerUri changed in appsettings, but timed out while waiting " +
                        "for value to change in the options monitor.");
                    return;
                }

                await _messenger.SendGenericMessage(GenericMessageKind.ServerUriChanged);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update settings.");
        }
    }
}