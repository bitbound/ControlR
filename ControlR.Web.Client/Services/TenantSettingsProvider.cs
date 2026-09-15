using ControlR.Libraries.Api.Contracts.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

namespace ControlR.Web.Client.Services;

public interface ITenantSettingsProvider
{
  Task<bool> GetAppendInstanceId();
  Task<string?> GetInstanceId();
  Task<bool?> GetNotifyUserOnSessionStart();
  Task<TenantSettingsDto> GetSettings();
  Task<bool> SetAppendInstanceId(bool value);
  Task<bool> SetInstanceId(string? value);
  Task<bool> SetNotifyUserOnSessionStart(bool? value);
}

internal class TenantSettingsProvider(
  IControlrApi controlrApi,
  AuthenticationStateProvider authState,
  IEffectiveUserPreferences effectiveUserPreferences,
  ISnackbar snackbar,
  ILogger<TenantSettingsProvider> logger) : ITenantSettingsProvider
{
  private readonly AuthenticationStateProvider _authState = authState;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IEffectiveUserPreferences _effectiveUserPreferences = effectiveUserPreferences;
  private readonly ILogger<TenantSettingsProvider> _logger = logger;
  private readonly ISnackbar _snackbar = snackbar;

  private TenantSettingsDto? _settings;

  public async Task<bool> GetAppendInstanceId()
  {
    var settings = await GetSettings();
    return settings.AppendInstanceId ?? false;
  }

  public async Task<string?> GetInstanceId()
  {
    var settings = await GetSettings();
    return settings.InstanceId;
  }

  public async Task<bool?> GetNotifyUserOnSessionStart()
  {
    var settings = await GetSettings();
    return settings.NotifyUserOnSessionStart;
  }

  public async Task<TenantSettingsDto> GetSettings()
  {
    if (_settings is not null)
    {
      return _settings;
    }

    if (await GetTenantId() is not { } tenantId)
    {
      return CreateDefaultSettings();
    }

    var getResult = await _controlrApi.V1.TenantSettings.GetTenantSettings(tenantId);
    if (!getResult.IsSuccess)
    {
      _snackbar.Add(getResult.Reason, Severity.Error);
      return CreateDefaultSettings();
    }

    _settings = getResult.Value;
    return _settings ?? CreateDefaultSettings();
  }

  public async Task<bool> SetAppendInstanceId(bool value)
  {
    return await SetSetting(TenantSettingNames.AppendInstanceId, value);
  }

  public async Task<bool> SetInstanceId(string? value)
  {
    var normalizedValue = string.IsNullOrWhiteSpace(value)
      ? null
      : value.Trim();

    if (!string.IsNullOrWhiteSpace(normalizedValue))
    {
      var normalizationResult = TenantSettingDefinitions.Normalize(TenantSettingNames.InstanceId, normalizedValue);
      if (!normalizationResult.IsSuccess)
      {
        _logger.LogWarning("Rejected invalid instance ID. Reason: {ValidationError}", normalizationResult.ErrorMessage);
        _snackbar.Add(normalizationResult.ErrorMessage ?? "Invalid instance ID.", Severity.Error);
        return false;
      }

      normalizedValue = normalizationResult.Value;
    }

    return await SetSetting(TenantSettingNames.InstanceId, normalizedValue);
  }

  public async Task<bool> SetNotifyUserOnSessionStart(bool? value)
  {
    return await SetSetting(TenantSettingNames.NotifyUserOnSessionStart, value);
  }

  private static TenantSettingsDto CreateDefaultSettings()
  {
    Dictionary<string, string> values = [];
    var defaults = TenantSettingDefinitions.CreateDto(values);
    return new TenantSettingsDto(
      defaults.AppendInstanceId,
      defaults.InstanceId,
      defaults.NotifyUserOnSessionStart);
  }

  private async Task<Guid?> GetTenantId()
  {
    var state = await _authState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(out var tenantId) ? tenantId : null;
  }

  private async Task<bool> SetSetting<T>(string settingName, T newValue)
  {
    try
    {
      if (await GetTenantId() is not { } tenantId)
      {
        _snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
        return false;
      }

      if (newValue is null)
      {
        var deleteResult = await _controlrApi.V1.TenantSettings.DeleteTenantSetting(settingName, tenantId);
        if (!deleteResult.IsSuccess)
        {
          _logger.LogError("Failed to delete setting.  Reason: {Reason}, StatusCode: {StatusCode}",
            deleteResult.Reason,
            deleteResult.StatusCode);

          _snackbar.Add(deleteResult.Reason, Severity.Error);
          return false;
        }

        _settings = null;
        _effectiveUserPreferences.InvalidateCache();
        return true;
      }

      var stringValue = TenantSettingDefinitions.FormatValue(settingName, newValue)?.Trim();
      Guard.IsNotNull(stringValue);
      var normalizationResult = TenantSettingDefinitions.Normalize(settingName, stringValue);
      if (!normalizationResult.IsSuccess)
      {
        _logger.LogWarning("Failed to normalize setting {SettingName}. Reason: {Reason}", settingName, normalizationResult.ErrorMessage);
        _snackbar.Add(normalizationResult.ErrorMessage ?? "Setting value is invalid.", Severity.Error);
        return false;
      }

      var request = new TenantSettingRequestDto(settingName, normalizationResult.Value ?? string.Empty);
      var setResult = await _controlrApi.V1.TenantSettings.SetTenantSetting(tenantId, request);

      if (!setResult.IsSuccess)
      {
        _logger.LogError("Failed to set setting.  Reason: {Reason}, StatusCode: {StatusCode}",
          setResult.Reason,
          setResult.StatusCode);

        _snackbar.Add(setResult.Reason, Severity.Error);
        return false;
      }

      _settings = null;
      _effectiveUserPreferences.InvalidateCache();
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting setting for {SettingName}.", settingName);
      _snackbar.Add("Error while setting setting", Severity.Error);
      return false;
    }
  }
}
