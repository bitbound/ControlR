using System.Collections.Concurrent;
using ControlR.Libraries.Shared.Constants;
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

  public async Task<bool> GetHideOfflineDevices()
  {
    return await GetPref(UserPreferenceNames.HideOfflineDevices, true);
  }

  public async Task<bool> GetNotifyUserOnSessionStart()
  {
    return await GetPref(UserPreferenceNames.NotifyUserOnSessionStart, true);
  }

  public async Task<ThemeMode> GetThemeMode()
  {
    return await GetPref(UserPreferenceNames.ThemeMode, ThemeMode.Auto);
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

  public async Task SetThemeMode(ThemeMode value)
  {
    await SetPref(UserPreferenceNames.ThemeMode, value);
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
        TenantId = user.TenantId
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
