using System.Collections.Concurrent;
using System.Net;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using MudBlazor;

namespace ControlR.Web.Client.Services;

public interface ITenantSettingsProvider
{
  Task<bool?> GetNotifyUserOnSessionStart();
  Task SetNotifyUserOnSessionStart(bool? value);
}

internal class TenantSettingsProvider(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<TenantSettingsProvider> logger) : ITenantSettingsProvider
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<TenantSettingsProvider> _logger = logger;

  private readonly ConcurrentDictionary<string, object?> _settings = new();
  private readonly ISnackbar _snackbar = snackbar;

  public async Task<bool?> GetNotifyUserOnSessionStart()
  {
    return await GetSetting(TenantSettingsNames.NotifyUserOnSessionStart, (bool?)null);
  }

  public async Task SetNotifyUserOnSessionStart(bool? value)
  {
    await SetSetting(TenantSettingsNames.NotifyUserOnSessionStart, value);
  }

  private async Task<T> GetSetting<T>(string settingName, T defaultValue)
  {
    try
    {
      if (_settings.TryGetValue(settingName, out var value) &&
          value is T typedValue)
      {
        return typedValue;
      }

      var getResult = await _controlrApi.GetTenantSetting(settingName);

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

      _settings[settingName] = typedResult;
      return typedResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting setting for {SettingName}.", settingName);
      _snackbar.Add("Error while getting setting", Severity.Error);
      return defaultValue;
    }
  }

  private async Task SetSetting<T>(string settingName, T newValue)
  {
    try
    {
      _settings[settingName] = newValue;
      
      if (newValue is null)
      {
        var deleteResult = await _controlrApi.DeleteTenantSetting(settingName);
        if (!deleteResult.IsSuccess)
        {
          deleteResult.Log(_logger);
          _snackbar.Add(deleteResult.Reason, Severity.Error);
        }
        return;
      }
      
      var stringValue = Convert.ToString(newValue);
      Guard.IsNotNull(stringValue);
      var setResult = await _controlrApi.SetTenantSetting(settingName, stringValue);

      if (!setResult.IsSuccess)
      {
        setResult.Log(_logger);
        _snackbar.Add(setResult.Reason, Severity.Error);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting setting for {SettingName}.", settingName);
      _snackbar.Add("Error while setting setting", Severity.Error);
    }
  }
}
