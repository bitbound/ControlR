using System.Runtime.CompilerServices;

namespace ControlR.Viewer.Services;

public interface ISettings
{
  bool AppendInstanceIdToAgentInstall { get; set; }
  bool HideOfflineDevices { get; set; }
  bool NotifyUserSessionStart { get; set; }
  Uri ServerUri { get; set; }
  string Username { get; set; }
  Uri ViewerDownloadUri { get; }

  Task<Result<byte[]>> GetSecurePrivateKey();

  Task Reset();
  Task StoreSecurePrivateKey(byte[] privateKey);
}

internal class Settings(
  ISecureStorage secureStorage,
  IPreferences preferences,
  IMessenger messenger,
  IAppState appState,
  ILogger<Settings> logger) : ISettings
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
    }
  }

  public string Username
  {
    get => GetPref(string.Empty);
    set => SetPref(value);
  }

  public Uri ViewerDownloadUri => new(ServerUri, $"/downloads/{AppConstants.ViewerFileName}");

  public async Task<Result<byte[]>> GetSecurePrivateKey()
  {
    try
    {
      var stored = await secureStorage.GetAsync(PrivateKeyStorageKey);
      if (string.IsNullOrWhiteSpace(stored))
      {
        return Result.Fail<byte[]>("Stored key is empty.");
      }

      return Result.Ok(Convert.FromBase64String(stored));
    }
    catch (Exception ex)
    {
      var result = Result.Fail<byte[]>(ex, "Error while getting private key from secure storage.");
      logger.LogResult(result);
      secureStorage.Remove(PrivateKeyStorageKey);
      return result;
    }
  }

  public async Task Reset()
  {
    try
    {
      preferences.Clear();
      await appState.ClearKeys();
      secureStorage.Remove(PrivateKeyStorageKey);
      secureStorage.RemoveAll();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while clearing settings.");
    }
  }

  public async Task StoreSecurePrivateKey(byte[] privateKey)
  {
    try
    {
      await secureStorage.SetAsync(PrivateKeyStorageKey, Convert.ToBase64String(privateKey));
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting private key from secure storage.");
      secureStorage.Remove(PrivateKeyStorageKey);
    }
  }

  private T GetPref<T>(T defaultValue, [CallerMemberName] string callerMemberName = "")
  {
    try
    {
      return preferences.Get(callerMemberName, defaultValue);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting preference for {MemberName}.", callerMemberName);
      return defaultValue;
    }
  }

  private void SetPref<T>(T newValue, [CallerMemberName] string callerMemmberName = "")
  {
    preferences.Set(callerMemmberName, newValue);
  }
}