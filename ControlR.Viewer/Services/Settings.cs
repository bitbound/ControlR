using CommunityToolkit.Mvvm.Messaging;
using ControlR.Shared;
using ControlR.Shared.Models;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models.Messages;

namespace ControlR.Viewer.Services;

internal interface ISettings
{
    string KeypairExportPath { get; set; }
    byte[] PrivateKey { get; set; }
    byte[] PublicKey { get; set; }
    string PublicKeyBase64 { get; }
    bool RememberPassphrase { get; set; }
    string Username { get; set; }
    Task Clear();

    Task<byte[]> GetEncryptedPrivateKey();
    Task<string> GetPassphrase();
    Task SetEncryptedPrivateKey(byte[] value);
    Task SetPassphrase(string passphrase);
    Task UpdateKeypair(string username, UserKeyPair keypair);
    Task UpdateKeypair(KeypairExport export);
}

internal class Settings(
    ISecureStorage secureStorage,
    IPreferences preferences,
    IMessenger messenger) : ISettings
{
    private readonly IMessenger _messenger = messenger;
    private readonly IPreferences _preferences = preferences;
    private readonly ISecureStorage _secureStorage = secureStorage;
    private byte[] _privateKey = [];

    public string KeypairExportPath
    {
        get => _preferences.Get(nameof(KeypairExportPath), string.Empty);
        set => _preferences.Set(nameof(KeypairExportPath), value);
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
        get => _preferences.Get(nameof(PublicKeyBase64), string.Empty);
        set => _preferences.Set(nameof(PublicKeyBase64), value);
    }

    public bool RememberPassphrase
    {
        get => _preferences.Get(nameof(RememberPassphrase), false);
        set => _preferences.Set(nameof(RememberPassphrase), value);
    }

    public string ServerUri => _preferences.Get(nameof(ServerUri), AppConstants.ServerUri);

    public string Username
    {
        get => _preferences.Get(nameof(Username), string.Empty);
        set => _preferences.Set(nameof(Username), value);
    }

    public Task Clear()
    {
        RememberPassphrase = false;
        PrivateKey = [];
        PublicKey = [];
        _secureStorage.RemoveAll();
        _messenger.SendParameterlessMessage(ParameterlessMessageKind.AuthStateChanged);
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
        _messenger.SendParameterlessMessage(ParameterlessMessageKind.AuthStateChanged);
    }

    public async Task UpdateKeypair(KeypairExport export)
    {
        Username = export.Username;
        PublicKey = export.PublicKey;
        await SetEncryptedPrivateKey(export.EncryptedPrivateKey);
        _messenger.SendParameterlessMessage(ParameterlessMessageKind.AuthStateChanged);
    }
}
