﻿using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Models;
using ControlR.Shared.Services;
using Microsoft.Extensions.Logging;
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

    string GetAppSettingsPath();

    Task UpdateSettings(AgentAppSettings settings);
}

internal class SettingsProvider(
    IOptionsMonitor<AgentAppOptions> _appOptions,
    IMessenger _messenger,
    IDelayer _delayer,
    IFileSystem _fileSystem,
    ILogger<SettingsProvider> _logger) : ISettingsProvider
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public static string AppSettingsPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var settingsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ControlR");

                if (EnvironmentHelper.Instance.IsDebug)
                {
                    settingsDir = Path.Combine(settingsDir, "Debug");
                }
                var dir = Directory.CreateDirectory(settingsDir).FullName;
                return Path.Combine(dir, "appsettings.json");
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var settingsDir = "/etc/controlr";
                if (EnvironmentHelper.Instance.IsDebug)
                {
                    settingsDir += "/debug";
                }
                var dir = Directory.CreateDirectory(settingsDir).FullName;
                return Path.Combine(dir, "appsettings.json");
            }

            throw new PlatformNotSupportedException();
        }
    }

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

    public string GetAppSettingsPath()
    {
        return AppSettingsPath;
    }

    public async Task UpdateSettings(AgentAppSettings settings)
    {
        try
        {
            var serverUriChanged =
               Uri.TryCreate(settings.AppOptions.ServerUri, UriKind.Absolute, out var newServerUri) &&
               newServerUri != ServerUri;

            var content = JsonSerializer.Serialize(settings, _jsonOptions);
            await _fileSystem.WriteAllTextAsync(AppSettingsPath, content);

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

                await _messenger.SendGenericMessage(Viewer.Models.Messages.GenericMessageKind.ServerUriChanged);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update settings.");
        }
    }
}