using ControlR.Libraries.Api.Contracts.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

namespace ControlR.Web.Client.Services;

public interface IEffectiveUserPreferences
{
  Task<EffectivePreference<bool>> GetNotifyUserOnSessionStart();
  void InvalidateCache();
}

internal sealed class EffectiveUserPreferences(
  IControlrApi controlrApi,
  AuthenticationStateProvider authState,
  ILogger<EffectiveUserPreferences> logger,
  ISnackbar snackbar) : IEffectiveUserPreferences
{
  private readonly AuthenticationStateProvider _authState = authState;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<EffectiveUserPreferences> _logger = logger;
  private readonly ISnackbar _snackbar = snackbar;
  private EffectiveUserPreferencesDto? _preferences;

  public async Task<EffectivePreference<bool>> GetNotifyUserOnSessionStart()
  {
    try
    {
      if (_preferences is null)
      {
        if (await _authState.GetTenantIdAsync() is not { } tenantId)
        {
          return new EffectivePreference<bool>(EffectivePreferenceDefinitions.NotifyUserOnSessionStart.DefaultValue, false);
        }

        var result = await _controlrApi.V1.EffectiveUserPreferences.GetEffectiveUserPreferences(tenantId);
        if (!result.IsSuccess)
        {
          _snackbar.Add(result.Reason, Severity.Error);
          return new EffectivePreference<bool>(EffectivePreferenceDefinitions.NotifyUserOnSessionStart.DefaultValue, false);
        }

        _preferences = result.Value ??
          new EffectiveUserPreferencesDto(EffectivePreferenceDefinitions.NotifyUserOnSessionStart.DefaultValue, false);
      }

      return new EffectivePreference<bool>(
        _preferences.NotifyUserOnSessionStart,
        _preferences.IsNotifyUserOnSessionStartTenantEnforced);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting effective user preferences.");
      _snackbar.Add("Error while getting effective user preferences", Severity.Error);
      return new EffectivePreference<bool>(EffectivePreferenceDefinitions.NotifyUserOnSessionStart.DefaultValue, false);
    }
  }

  public void InvalidateCache()
  {
    _preferences = null;
  }
}
