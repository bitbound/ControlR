using System.Collections.Concurrent;
using System.Net;

namespace ControlR.Web.Client.Services;

public interface IUserSettingsProvider
{
  Task<bool> GetHideOfflineDevices();
  Task<bool> GetNotifyUserOnSessionStart();
  Task<string> GetUserDisplayName();
  Task SetHideOfflineDevices(bool value);
  Task SetNotifyUserOnSessionStart(bool value);
  Task SetUserDisplayName(string value);
}

internal class UserSettingsProvider(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserSettingsProvider> logger) : IUserSettingsProvider
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<UserSettingsProvider> _logger = logger;

  private readonly ConcurrentDictionary<string, object?> _preferences = new();
  private readonly ISnackbar _snackbar = snackbar;

  public async Task<bool> GetHideOfflineDevices()
  {
    return await GetPref(UserPreferenceNames.HideOfflineDevices, true);
  }

  public async Task<bool> GetNotifyUserOnSessionStart()
  {
    return await GetPref(UserPreferenceNames.NotifyUserOnSessionStart, true);
  }

  public async Task<string> GetUserDisplayName()
  {
    return await GetPref(UserPreferenceNames.UserDisplayName, string.Empty);
  }

  public async Task SetHideOfflineDevices(bool value)
  {
    await SetPref(UserPreferenceNames.HideOfflineDevices, value);
  }

  public async Task SetNotifyUserOnSessionStart(bool value)
  {
    await SetPref(UserPreferenceNames.NotifyUserOnSessionStart, value);
  }

  public async Task SetUserDisplayName(string value)
  {
    await SetPref(UserPreferenceNames.UserDisplayName, value);
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

      var targetType = typeof(T);

      if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        // Get the underlying type (e.g., bool from bool?)
        targetType = Nullable.GetUnderlyingType(targetType) ??
          throw new InvalidOperationException($"Failed to convert setting value to type {targetType.Name}.");
      }

      if (Convert.ChangeType(getResult.Value.Value, targetType) is not T typedResult)
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