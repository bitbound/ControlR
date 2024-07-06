using System.Runtime.CompilerServices;

namespace ControlR.Viewer.Services;

public interface ISettings
{
    bool AppendInstanceIdToAgentInstall { get; set; }
    bool HideOfflineDevices { get; set; }
    bool NotifyUserSessionStart { get; set; }
    string PublicKeyLabel { get; set; }
    Uri ServerUri { get; set; }
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

    public bool AppendInstanceIdToAgentInstall
    {
        get => GetPref(false);
        set => SetPref(value);
    }

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

    public string PublicKeyLabel
    {
        get
        {
            var pref = GetPref("");
            if (!string.IsNullOrWhiteSpace(pref))
            {
                return pref;
            }
            return Username;
        }
        set => SetPref(value);
    }

    public Uri ServerUri
    {
        get
        {
            if (Uri.TryCreate(GetPref(""), UriKind.Absolute, out var uri))
            {
                return uri;
            }
            return AppConstants.ServerUri;
        }
        set
        {
            SetPref($"{value}".TrimEnd('/'));
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
            _preferences.Clear();
            await _appState.ClearKeys();
            _secureStorage.Remove(PrivateKeyStorageKey);
            _secureStorage.RemoveAll();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while clearing settings.");
        }
    }

    public async Task StoreSecurePrivateKey(byte[] privateKey)
    {
        try
        {
            await _secureStorage.SetAsync(PrivateKeyStorageKey, Convert.ToBase64String(privateKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting private key from secure storage.");
            _secureStorage.Remove(PrivateKeyStorageKey);
        }
    }

    private T GetPref<T>(T defaultValue, [CallerMemberName] string callerMemberName = "")
    {
        try
        {
            return _preferences.Get(callerMemberName, defaultValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting preference for {MemberName}.", callerMemberName);
            return defaultValue;
        }
    }

    private void SetPref<T>(T newValue, [CallerMemberName] string callerMemmberName = "")
    {
        _preferences.Set(callerMemmberName, newValue);
    }
}