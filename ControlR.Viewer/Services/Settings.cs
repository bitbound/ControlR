using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
using ControlR.Devices.Common.Messages;
using ControlR.Shared;
using ControlR.Shared.Models;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace ControlR.Viewer.Services;

public interface ISettings
{
    bool HideOfflineDevices { get; set; }
    bool NotifyUserSessionStart { get; set; }
    byte[] PrivateKey { get; set; }
    byte[] PublicKey { get; set; }
    string PublicKeyBase64 { get; }
    bool RememberPassphrase { get; set; }
    string ServerUri { get; set; }
    string Username { get; set; }
    string ViewerDownloadUri { get; }

    Task Clear();

    Task<byte[]> GetEncryptedPrivateKey();

    Task<string> GetPassphrase();

    Task SetEncryptedPrivateKey(byte[] value);

    Task SetPassphrase(string passphrase);

    Task UpdateKeypair(string username, UserKeyPair keypair);

    Task UpdateKeypair(KeypairExport export);
}

internal class Settings(
    ISecureStorage _secureStorage,
    IPreferences _preferences,
    IMessenger _messenger,
    ILogger<Settings> _logger) : ISettings
{
    private byte[] _privateKey = [];

    public bool HideOfflineDevices
    {
        get => GetPref(true);
        set => SetPref(value);
    }

    public bool NotifyUserSessionStart
    {
        get => GetPref(false);
        set => SetPref(value);
    }

    public byte[] PrivateKey
    {
        get => _privateKey;
        set => _privateKey = value;
    }

    public byte[] PublicKey
    {
        get => Convert.FromBase64String(PublicKeyBase64);
        set => PublicKeyBase64 = Convert.ToBase64String(value);
    }

    public string PublicKeyBase64
    {
        get => GetPref(string.Empty);
        set => SetPref(value);
    }

    public bool RememberPassphrase
    {
        get => GetPref(false);
        set => SetPref(value);
    }
    public string ServerUri
    {
        get => GetPref(AppConstants.ServerUri).TrimEnd('/');
        set
        {
            SetPref(value.TrimEnd('/'));
            _messenger.SendGenericMessage(GenericMessageKind.ServerUriChanged).Forget();
        }
    }

    public string Username
    {
        get => GetPref(string.Empty);
        set => SetPref(value);
    }

    public string ViewerDownloadUri
    {
        get
        {
            return $"{ServerUri}/downloads/{AppConstants.ViewerFileName}";
        }
    }

    public Task Clear()
    {
        try
        {
            RememberPassphrase = false;
            PrivateKey = [];
            PublicKey = [];
            _secureStorage.RemoveAll();
            _messenger.SendGenericMessage(GenericMessageKind.AuthStateChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while clearing settings.");
        }
        return Task.CompletedTask;
    }

    public async Task<byte[]> GetEncryptedPrivateKey()
    {
        var stored = await _secureStorage.GetAsync("EncryptedPrivateKey");
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }
        return Convert.FromBase64String(stored);
    }

    public async Task<string> GetPassphrase()
    {
        return await _secureStorage.GetAsync("Passphrase") ?? string.Empty;
    }

    public async Task SetEncryptedPrivateKey(byte[] value)
    {
        await _secureStorage.SetAsync("EncryptedPrivateKey", Convert.ToBase64String(value));
    }

    public async Task SetPassphrase(string passphrase)
    {
        await _secureStorage.SetAsync("Passphrase", passphrase);
    }

    public async Task UpdateKeypair(string username, UserKeyPair keypair)
    {
        Username = username;
        PublicKey = keypair.PublicKey;
        PrivateKey = keypair.PrivateKey;
        await SetEncryptedPrivateKey(keypair.EncryptedPrivateKey);
        await _messenger.SendGenericMessage(GenericMessageKind.AuthStateChanged);
    }

    public async Task UpdateKeypair(KeypairExport export)
    {
        Username = export.Username;
        PublicKey = Convert.FromBase64String(export.PublicKey);
        await SetEncryptedPrivateKey(Convert.FromBase64String(export.EncryptedPrivateKey));
        await _messenger.SendGenericMessage(GenericMessageKind.AuthStateChanged);
    }

    private T GetPref<T>(T defaultValue, [CallerMemberName] string callerMemberName = "")
    {
        return _preferences.Get<T>(callerMemberName, defaultValue);
    }

    private void SetPref<T>(T newValue, [CallerMemberName] string callerMemmberName = "")
    {
        _preferences.Set(callerMemmberName, newValue);
    }
}