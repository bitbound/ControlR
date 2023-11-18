using ControlR.Agent.Models;
using ControlR.Shared;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services;

internal interface ISettingsProvider
{
    IReadOnlyList<string> AuthorizedKeys { get; }
    bool AutoRunVnc { get; }
    string DeviceId { get; }
    Uri ServerUri { get; }
    int VncPort { get; }
}

internal class SettingsProvider(IOptionsMonitor<AppOptions> _appOptions) : ISettingsProvider
{
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
}