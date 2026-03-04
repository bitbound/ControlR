using System.Collections.Concurrent;
using System.Net;
using ControlR.Web.Client.Constants;
using ControlR.Web.Client.Models;

namespace ControlR.Web.Client.Services;

public interface IUserSettingsProvider
{
  Task<bool> GetHideOfflineDevices();
  Task<KeyboardInputMode> GetKeyboardInputMode();
  Task<bool> GetNotifyUserOnSessionStart();
  Task<ThemeMode> GetThemeMode();
  Task<string> GetUserDisplayName();
  Task SetHideOfflineDevices(bool value);
  Task SetKeyboardInputMode(KeyboardInputMode value);
  Task SetNotifyUserOnSessionStart(bool value);
  Task SetThemeMode(ThemeMode value);
  Task SetUserDisplayName(string value);
}

internal class UserSettingsProviderClient(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserSettingsProviderClient> logger) : IUserSettingsProvider
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<UserSettingsProviderClient> _logger = logger;

  private readonly ConcurrentDictionary<string, object?> _preferences = new();
  private readonly ISnackbar _snackbar = snackbar;

  public Task<bool> GetHideOfflineDevices()
  {
    return GetPref(UserPreferenceNames.HideOfflineDevices, true);
  }

  public Task<KeyboardInputMode> GetKeyboardInputMode()
  {
    return GetPref(UserPreferenceNames.KeyboardInputMode, KeyboardInputMode.Auto);
  }

  public Task<bool> GetNotifyUserOnSessionStart()
  {
    return GetPref(UserPreferenceNames.NotifyUserOnSessionStart, true);
  }

  public Task<ThemeMode> GetThemeMode()
  {
    return GetPref(UserPreferenceNames.ThemeMode, ThemeMode.Auto);
  }

  public Task<string> GetUserDisplayName()
  {
    return GetPref(UserPreferenceNames.UserDisplayName, string.Empty);
  }

  public Task SetHideOfflineDevices(bool value)
  {
    return SetPref(UserPreferenceNames.HideOfflineDevices, value);
  }

  public Task SetKeyboardInputMode(KeyboardInputMode value)
  {
    return SetPref(UserPreferenceNames.KeyboardInputMode, value);
  }

  public Task SetNotifyUserOnSessionStart(bool value)
  {
    return SetPref(UserPreferenceNames.NotifyUserOnSessionStart, value);
  }

  public Task SetThemeMode(ThemeMode value)
  {
    return SetPref(UserPreferenceNames.ThemeMode, value);
  }

  public Task SetUserDisplayName(string value)
  {
    return SetPref(UserPreferenceNames.UserDisplayName, value);
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

      var getResult = await _controlrApi.UserPreferences.GetUserPreference(preferenceName);

      if (!getResult.IsSuccess)
      {
        if (getResult.StatusCode == HttpStatusCode.NotFound)
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

      if (!getResult.Value.HasValueSet)
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

      if (targetType.IsEnum)
      {
        if (Enum.TryParse(targetType, getResult.Value.Value, true, out var enumValue))
        {
          _preferences[preferenceName] = enumValue;
          return (T)enumValue;
        }
        else
        {
          _logger.LogError(
            "Failed to parse enum preference {PreferenceName} with value {PreferenceValue} to type {TargetType}.",
            preferenceName,
            getResult.Value.Value,
            targetType.Name);

          return defaultValue;
        }
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
      var stringValue = Convert.ToString(newValue)?.Trim();
      Guard.IsNotNull(stringValue);
      var request = new UserPreferenceRequestDto(preferenceName, stringValue);
      var setResult = await _controlrApi.UserPreferences.SetUserPreference(request);

      if (!setResult.IsSuccess)
      {
        _logger.LogError("Failed to set preference.  Reason: {Reason}, StatusCode: {StatusCode}",
          setResult.Reason,
          setResult.StatusCode);
          
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