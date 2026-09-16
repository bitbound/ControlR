using ControlR.Libraries.Api.Contracts.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using V1UserPreferencesDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences.UserPreferencesDto;
using V1UserPreferenceRequestDto = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences.UserPreferenceRequestDto;

namespace ControlR.Web.Client.Services;

public interface IUserPreferencesProvider
{
  Task<InternalDtos.UserPreferencesDto> GetPreferences();
  Task SetPreference<T>(string preferenceName, T value);
}

internal class UserPreferencesProviderClient(
  IControlrApi controlrApi,
  AuthenticationStateProvider authState,
  ISnackbar snackbar,
  ILogger<UserPreferencesProviderClient> logger) : IUserPreferencesProvider
{
  private readonly AuthenticationStateProvider _authState = authState;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<UserPreferencesProviderClient> _logger = logger;
  private readonly ISnackbar _snackbar = snackbar;

  private InternalDtos.UserPreferencesDto? _preferences;

  public async Task<InternalDtos.UserPreferencesDto> GetPreferences()
  {
    try
    {
      if (_preferences is not null)
      {
        return _preferences;
      }

      if (await _authState.GetTenantId() is not { } tenantId)
      {
        return CreateDefaultPreferences();
      }

      var getResult = await _controlrApi.V1.UserPreferences.GetPreferences(tenantId);
      if (!getResult.IsSuccess)
      {
        _snackbar.Add(getResult.Reason, Severity.Error);
        return CreateDefaultPreferences();
      }

      _preferences = getResult.Value is { } fetched ? ToInternalDto(fetched) : CreateDefaultPreferences();
      return _preferences;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting preferences.");
      _snackbar.Add("Error while getting preference", Severity.Error);
      return CreateDefaultPreferences();
    }
  }

  public async Task SetPreference<T>(string preferenceName, T value)
  {
    try
    {
      if (await _authState.GetTenantId() is not { } tenantId)
      {
        _logger.LogWarning("Cannot set preference {PreferenceName} - no tenant claim on the signed-in user.", preferenceName);
        return;
      }

      var stringValue = UserPreferenceDefinitions.FormatValue(preferenceName, value)?.Trim();
      Guard.IsNotNull(stringValue);
      var normalizationResult = UserPreferenceDefinitions.Normalize(preferenceName, stringValue);
      if (!normalizationResult.IsSuccess)
      {
        _logger.LogWarning("Failed to normalize preference {PreferenceName}. Reason: {Reason}", preferenceName, normalizationResult.ErrorMessage);
        _snackbar.Add(normalizationResult.ErrorMessage ?? "Preference value is invalid.", Severity.Error);
        return;
      }

      var request = new V1UserPreferenceRequestDto(preferenceName, normalizationResult.Value ?? string.Empty);
      var setResult = await _controlrApi.V1.UserPreferences.SetPreference(tenantId, request);

      if (!setResult.IsSuccess)
      {
        _logger.LogError("Failed to set preference.  Reason: {Reason}, StatusCode: {StatusCode}",
          setResult.Reason,
          setResult.StatusCode);

        _snackbar.Add(setResult.Reason, Severity.Error);
        return;
      }

      _preferences = null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while setting preference for {PreferenceName}.", preferenceName);
      _snackbar.Add("Error while setting preference", Severity.Error);
    }
  }

  private static InternalDtos.UserPreferencesDto CreateDefaultPreferences()
  {
    Dictionary<string, string> values = [];
    return UserPreferenceDefinitions.CreateDto(values);
  }

  /// <summary>
  /// Translates the V1 wire shape into the internal DTO the rest of the client works in, so the
  /// stable contract stops at the API boundary instead of leaking into components and view models.
  /// </summary>
  private static InternalDtos.UserPreferencesDto ToInternalDto(V1UserPreferencesDto preferences)
  {
    return new InternalDtos.UserPreferencesDto(
      preferences.AutoQualityLowerThresholdMbps,
      preferences.AutoQualityMaximum,
      preferences.AutoQualityMinimum,
      preferences.AutoQualityUpperThresholdMbps,
      preferences.CaptureCursor,
      preferences.EncodingFormat,
      preferences.EnableDirectX,
      preferences.HideOfflineDevices,
      preferences.ShowOnlyUntaggedDevices,
      preferences.ShowOnlyUngroupedDevices,
      preferences.IsAutoQualityEnabled,
      preferences.IsMaxBandwidthEnabled,
      preferences.KeyboardInputMode,
      preferences.ManualQuality,
      preferences.MaxBandwidthMbps,
      preferences.NotifyUserOnSessionStart,
      preferences.OpenDeviceInNewTab,
      preferences.ThemeMode,
      preferences.UserDisplayName,
      preferences.ViewMode);
  }
}
