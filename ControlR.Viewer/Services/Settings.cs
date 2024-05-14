using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Extensions;
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
 
    string ServerUri { get; set; }
    string Username { get; set; }

    string ViewerDownloadUri { get; }

    Task<Result<byte[]>> GetSecurePrivateKey();

    Task Reset();
    Task StoreSecurePrivateKey(byte[] privateKey);

}

internal class Settings(
    ISecureStorage _secureStorage,
    IPreferences _preferences,
    IMessenger _messenger,
    IAppState _appState,
    ILogger<Settings> _logger) : ISettings
{
    private const string PrivateKeyStorageKey = "SecurePrivateKey";

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
    public async Task<Result<byte[]>> GetSecurePrivateKey()
    {
        try
        {
            var stored = await _secureStorage.GetAsync(PrivateKeyStorageKey);
            if (string.IsNullOrWhiteSpace(stored))
            {
                return Result.Fail<byte[]>("Stored key is empty.");
            }
            return Result.Ok(Convert.FromBase64String(stored));
        }
        catch (Exception ex)
        {
            var result = Result.Fail<byte[]>(ex, "Error while getting private key from secure storage.");
            _logger.LogResult(result);
            _secureStorage.Remove(PrivateKeyStorageKey);
            return result;
        }
    }

    public async Task Reset()
    {
        try
        {
            _secureStorage.RemoveAll();
            _preferences.Clear();
            await _appState.ClearKeys();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while clearing settings.");
        }
    }

    public async Task StoreSecurePrivateKey(byte[] privateKey)
    {
        await _secureStorage.SetAsync(PrivateKeyStorageKey, Convert.ToBase64String(privateKey));
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