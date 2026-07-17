using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Client.Models;

namespace ControlR.Web.Client.Services;

public interface IEffectiveUserPreferences
{
  Task<EffectivePreference<bool>> GetNotifyUserOnSessionStart();
  void InvalidateCache();
}

internal sealed class EffectiveUserPreferences(
  IControlrApi controlrApi,
  ILogger<EffectiveUserPreferences> logger,
  ISnackbar snackbar) : IEffectiveUserPreferences
{
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
        var result = await _controlrApi.Internal.EffectiveUserPreferences.GetEffectiveUserPreferences();
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