using ControlR.Agent.Options;
using ControlR.Agent.Startup;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ControlR.Agent.Services;

internal interface ISettingsProvider
{
    IReadOnlyList<AuthorizedKeyDto> AuthorizedKeys { get; }
    string DeviceId { get; }
    bool IsConnectedToPublicServer { get; }
    Uri ServerUri { get; }
    string GetAppSettingsPath();
    Task UpdatePublicKeyLabel(string publicKeyBase64, string publicKeyLabel);
    Task UpdateSettings(AgentAppSettings settings);
}

internal class SettingsProvider(
    IOptionsMonitor<AgentAppOptions> _appOptions,
    IFileSystem _fileSystem,
    IOptions<InstanceOptions> _instanceOptions,
    ILogger<SettingsProvider> _logger) : ISettingsProvider
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _updateLock = new(1, 1);


    public IReadOnlyList<AuthorizedKeyDto> AuthorizedKeys
    {
        get => _appOptions.CurrentValue.AuthorizedKeys;
    }


    public string DeviceId
    {
        get => _appOptions.CurrentValue.DeviceId;
    }

    public Uri ServerUri
    {
        get
        {
            return 
                _appOptions.CurrentValue.ServerUri ??
                AppConstants.ServerUri ??
                throw new InvalidOperationException("Server URI is not configured correctly.");
        }
    }

    public bool IsConnectedToPublicServer => 
        _appOptions.CurrentValue.ServerUri?.Authority == AppConstants.ProdServerUri.Authority;

    public string GetAppSettingsPath()
    {
        return PathConstants.GetAppSettingsPath(_instanceOptions.Value.InstanceId);
    }

    public async Task UpdatePublicKeyLabel(string publicKeyBase64, string publicKeyLabel)
    {
        await _updateLock.WaitAsync();
        try
        {
            var authorizedKeys = AuthorizedKeys.ToList();

            var index = authorizedKeys.FindIndex(x => x.PublicKey == publicKeyBase64);
            if (index == -1)
            {
                _logger.LogWarning("Unable to update public key label.  Public key {PublicKey} not found.", publicKeyBase64);
                return;
            }

            authorizedKeys[index] = authorizedKeys[index] with { Label = publicKeyLabel };
            _appOptions.CurrentValue.AuthorizedKeys = [.. authorizedKeys];
            await WriteToDisk(_appOptions.CurrentValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update public key label.");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task UpdateSettings(AgentAppSettings settings)
    {
        await _updateLock.WaitAsync();
        try
        {
            await WriteToDisk(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update settings.");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task WriteToDisk(AgentAppOptions options)
    {
        await WriteToDisk(new AgentAppSettings() { AppOptions = options });
    }

    private async Task WriteToDisk(AgentAppSettings settings)
    {
        var content = JsonSerializer.Serialize(settings, _jsonOptions);
        await _fileSystem.WriteAllTextAsync(GetAppSettingsPath(), content);
    }
}