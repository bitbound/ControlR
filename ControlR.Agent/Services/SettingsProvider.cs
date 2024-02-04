using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ControlR.Agent.Services;

internal interface ISettingsProvider
{
    IReadOnlyList<string> AuthorizedKeys { get; }
    bool AutoRunVnc { get; }
    string DeviceId { get; }
    Uri ServerUri { get; }
    int VncPort { get; }

    Task UpdateSettings(AgentAppSettings settings);
}

internal class SettingsProvider(
    IOptionsMonitor<AgentAppOptions> _appOptions,
    IEnvironmentHelper _environment,
    IHostEnvironment _hostEnvironment,
    IMessenger _messenger,
    IFileSystem _fileSystem) : ISettingsProvider
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<string> AuthorizedKeys
    {
        get => _appOptions.CurrentValue.AuthorizedKeys ?? [];
    }

    public bool AutoRunVnc
    {
        get => _appOptions.CurrentValue.AutoRunVnc ?? false;
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

    public int VncPort
    {
        get => _appOptions.CurrentValue.VncPort ?? 5900;
    }

    public async Task UpdateSettings(AgentAppSettings settings)
    {
        var startupDir = _environment.StartupDirectory;
        var appsettingsPath = $"{startupDir}\\appsettings.json";
        var appsettingsEnvPath = $"{startupDir}\\appsettings.{_hostEnvironment.EnvironmentName}.json";
        var paths = new string[] { appsettingsPath, appsettingsEnvPath };
        var content = JsonSerializer.Serialize(settings, _jsonOptions);
        foreach (var path in paths)
        {
            if (_fileSystem.FileExists(path))
            {
                await _fileSystem.WriteAllTextAsync(path, content);
            }
        }

        if (Uri.TryCreate(settings.AppOptions.ServerUri, UriKind.Absolute, out var newServerUri) &&
            newServerUri != ServerUri)
        {
            await _messenger.SendGenericMessage(Viewer.Models.Messages.GenericMessageKind.ServerUriChanged);
        }
    }
}