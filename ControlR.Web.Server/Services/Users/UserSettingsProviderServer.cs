using System.Collections.Concurrent;
using ControlR.Libraries.Viewer.Common.Enums;
using ControlR.Web.Client.Constants;
using ControlR.Web.Client.Models;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Server.Services.Users;

internal class UserSettingsProviderServer(
  IDbContextFactory<AppDb> dbFactory,
  AuthenticationStateProvider authStateProvider,
  ILogger<UserSettingsProviderServer> logger) : IUserSettingsProvider
{
  private readonly AuthenticationStateProvider _authStateProvider = authStateProvider;
  private readonly IDbContextFactory<AppDb> _dbFactory = dbFactory;
  private readonly ILogger<UserSettingsProviderServer> _logger = logger;
  private readonly ConcurrentDictionary<string, object?> _preferences = new();

  public Task<bool> GetHideOfflineDevices()
  {
    return GetPref(UserPreferenceNames.HideOfflineDevices, true);
  }

  public Task<KeyboardInputMode> GetKeyboardInputMode()
  {
    return GetPref(UserPreferenceNames.KeyboardInputMode, KeyboardInputMode.Auto);
  }

  public Task<bool> GetOpenDeviceInNewTab()
  {
    return GetPref(UserPreferenceNames.OpenDeviceInNewTab, true);
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

  public Task<ViewMode> GetViewMode()
  {
    return GetPref(UserPreferenceNames.ViewMode, ViewMode.Fit);
  }

  public Task SetHideOfflineDevices(bool value)
  {
    return SetPref(UserPreferenceNames.HideOfflineDevices, value);
  }

  public Task SetKeyboardInputMode(KeyboardInputMode value)
  {
    return SetPref(UserPreferenceNames.KeyboardInputMode, value);
  }

  public Task SetOpenDeviceInNewTab(bool value)
  {
    return SetPref(UserPreferenceNames.OpenDeviceInNewTab, value);
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

  public Task SetViewMode(ViewMode value)
  {
    return SetPref(UserPreferenceNames.ViewMode, value);
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

      var authState = await _authStateProvider.GetAuthenticationStateAsync();
      if (!authState.User.TryGetUserId(out var userId))
      {
        return defaultValue;
      }

      var isAuthenticated = await _authStateProvider.IsAuthenticated();

      if (!isAuthenticated)
      {
        return defaultValue;
      }

      await using var db = await _dbFactory.CreateDbContextAsync();

      var preference = await db.UserPreferences
        .AsNoTracking()
        .Where(x => x.User!.Id == userId && x.Name == preferenceName)
        .FirstOrDefaultAsync();

      if (preference is null)
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
        if (Enum.TryParse(targetType, preference.Value, true, out var enumValue))
        {
          _preferences[preferenceName] = enumValue;
          return (T)enumValue;
        }

        _logger.LogError(
          "Failed to parse enum preference {PreferenceName} with value {PreferenceValue} to type {TargetType}.",
          preferenceName,
          preference.Value,
          targetType.Name);

        return defaultValue;
      }

      if (Convert.ChangeType(preference.Value, targetType) is not T typedResult)
      {
        return defaultValue;
      }

      _preferences[preferenceName] = typedResult;
      return typedResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting preference for {PreferenceName}.", preferenceName);
      return defaultValue;
    }
  }

  private async Task SetPref<T>(string preferenceName, T newValue)
  {
    try
    {
      _preferences[preferenceName] = newValue;
      var stringValue = Convert.ToString(newValue)?.Trim();
      
      if (string.IsNullOrEmpty(stringValue))
      {
        _logger.LogWarning("Cannot set preference {PreferenceName} - value is null or empty.", preferenceName);
        return;
      }

      var authState = await _authStateProvider.GetAuthenticationStateAsync();
      var userName = authState.User.Identity?.Name;

      if (string.IsNullOrEmpty(userName))
      {
        _logger.LogWarning("Cannot set preference {PreferenceName} - user is not authenticated.", preferenceName);
        return;
      }

      await using var db = await _dbFactory.CreateDbContextAsync();

      var user = await db.Users
        .Include(x => x.UserPreferences)
        .FirstOrDefaultAsync(x => x.UserName == userName);

      if (user is null)
      {
        _logger.LogWarning("Cannot set preference {PreferenceName} - user not found.", preferenceName);
        return;
      }

      user.UserPreferences ??= [];

      var index = user.UserPreferences.FindIndex(x => x.Name == preferenceName);

      var entity = new UserPreference
      {
        Name = preferenceName,
        Value = stringValue,
        UserId = user.Id,
      };

      if (index >= 0)
      {
        user.UserPreferences[index] = entity;
      }
      else
      {
        user.UserPreferences.Add(entity);
      }

      await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting preference for {PreferenceName}.", preferenceName);
    }
  }
}
