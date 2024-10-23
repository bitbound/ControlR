using System.Collections.Concurrent;
using System.Net;

namespace ControlR.Web.Client.Services;

public interface ISettings
{
  Task<bool> GetHideOfflineDevices();
  Task<bool> GetNotifyUserOnSessionStart();
  Task SetHideOfflineDevices(bool value);
  Task SetNotifyUserOnSessionStart(bool value);
}

internal class Settings(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<Settings> logger) : ISettings
{
  private const string _notifyUserOnSessionStartName = "notify-user-on-session-start";
  private const string _hideOfflineDevicesName = "hide-offline-devices";

  private readonly ConcurrentDictionary<string, object?> _preferences = new();

  public async Task<bool> GetHideOfflineDevices()
  {
    return await GetPref(_hideOfflineDevicesName, true);
  }

  public async Task SetHideOfflineDevices(bool value)
  {
    await SetPref(_hideOfflineDevicesName, value);
  }

  public async Task<bool> GetNotifyUserOnSessionStart()
  {
    return await GetPref(_notifyUserOnSessionStartName, true);
  }

  public async Task SetNotifyUserOnSessionStart(bool value)
  {
    await SetPref(_notifyUserOnSessionStartName, value);
  }


  private async Task<T> GetPref<T>(string preferenceName, T defaultValue)
  {
    try
    {
      if (_preferences.TryGetValue(preferenceName, out var value) &&
          value is T typedValue)
      {
        return typedValue;
      }

      var getResult = await controlrApi.GetUserPreference(preferenceName);

      if (!getResult.IsSuccess)
      {
        if (getResult.Exception is HttpRequestException httpEx && httpEx.StatusCode == HttpStatusCode.NotFound)
        {
          return defaultValue;
        }

        snackbar.Add(getResult.Reason, Severity.Error);
        return defaultValue;
      }

      if (Convert.ChangeType(getResult.Value.Value, typeof(T)) is T typedResult)
      {
        _preferences[preferenceName] = typedResult;
        return typedResult;
      }

      return defaultValue;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting preference for {PreferenceName}.", preferenceName);
      snackbar.Add("Error while getting preference", Severity.Error);
      return defaultValue;
    }
  }

  private async Task SetPref<T>(string preferenceName, T newValue)
  {
    try
    {
      _preferences[preferenceName] = newValue;
      var stringValue = Convert.ToString(newValue);
      Guard.IsNotNull(stringValue);
      var setResult = await controlrApi.SetUserPreference(preferenceName, stringValue);

      if (!setResult.IsSuccess)
      {
        setResult.Log(logger);
        snackbar.Add(setResult.Reason, Severity.Error);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while setting preference for {PreferenceName}.", preferenceName);
      snackbar.Add("Error while setting preference", Severity.Error);
    }
  }
}