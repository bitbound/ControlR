using System.Collections.Concurrent;
using System.Net;

namespace ControlR.Web.Client.Services;

public interface ISettings
{
  Task<bool> GetHideOfflineDevices();
  Task<bool> GetNotifyUserOnSessionStart();
  Task<string> GetUserDisplayName();
  Task SetUserDisplayName(string value);
  Task SetHideOfflineDevices(bool value);
  Task SetNotifyUserOnSessionStart(bool value);
}

internal class Settings(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<Settings> logger) : ISettings
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<Settings> _logger = logger;

  private readonly ConcurrentDictionary<string, object?> _preferences = new();
  private readonly ISnackbar _snackbar = snackbar;

  public async Task<bool> GetHideOfflineDevices()
  {
    return await GetPref(UserPreferenceNames.HideOfflineDevicesName, true);
  }

  public async Task SetUserDisplayName(string value)
  {
    await SetPref(UserPreferenceNames.UserDisplayName, value);
  }

  public async Task SetHideOfflineDevices(bool value)
  {
    await SetPref(UserPreferenceNames.HideOfflineDevicesName, value);
  }

  public async Task<bool> GetNotifyUserOnSessionStart()
  {
    return await GetPref(UserPreferenceNames.NotifyUserOnSessionStartName, true);
  }

  public async Task<string> GetUserDisplayName()
  {
    return await GetPref(UserPreferenceNames.UserDisplayName, string.Empty);
  }

  public async Task SetNotifyUserOnSessionStart(bool value)
  {
    await SetPref(UserPreferenceNames.NotifyUserOnSessionStartName, value);
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

      var getResult = await _controlrApi.GetUserPreference(preferenceName);

      if (!getResult.IsSuccess)
      {
        if (getResult.Exception is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
        {
          return defaultValue;
        }

        _snackbar.Add(getResult.Reason, Severity.Error);
        return defaultValue;
      }

      if (getResult.Value is null)
      {
        return defaultValue;
      }

      if (Convert.ChangeType(getResult.Value.Value, typeof(T)) is not T typedResult)
      {
        return defaultValue;
      }

      _preferences[preferenceName] = typedResult;
      return typedResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting preference for {PreferenceName}.", preferenceName);
      _snackbar.Add("Error while getting preference", Severity.Error);
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
      var setResult = await _controlrApi.SetUserPreference(preferenceName, stringValue);

      if (!setResult.IsSuccess)
      {
        setResult.Log(_logger);
        _snackbar.Add(setResult.Reason, Severity.Error);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting preference for {PreferenceName}.", preferenceName);
      _snackbar.Add("Error while setting preference", Severity.Error);
    }
  }
}