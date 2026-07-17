using ControlR.Libraries.Api.Contracts.Settings;

namespace ControlR.Web.Client.Services;

public interface IUserPreferencesProvider
{
  Task<UserPreferencesDto> GetPreferences();
  Task SetPreference<T>(string preferenceName, T value);
}

internal class UserPreferencesProviderClient(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserPreferencesProviderClient> logger) : IUserPreferencesProvider
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<UserPreferencesProviderClient> _logger = logger;
  private readonly ISnackbar _snackbar = snackbar;

  private UserPreferencesDto? _preferences;

  public async Task<UserPreferencesDto> GetPreferences()
  {
    try
    {
      if (_preferences is not null)
      {
        return _preferences;
      }

      var getResult = await _controlrApi.Internal.UserPreferences.GetUserPreferences();
      if (!getResult.IsSuccess)
      {
        _snackbar.Add(getResult.Reason, Severity.Error);
        return CreateDefaultPreferences();
      }

      _preferences = getResult.Value ?? CreateDefaultPreferences();
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
      var stringValue = UserPreferenceDefinitions.FormatValue(preferenceName, value)?.Trim();
      Guard.IsNotNull(stringValue);
      var normalizationResult = UserPreferenceDefinitions.Normalize(preferenceName, stringValue);
      if (!normalizationResult.IsSuccess)
      {
        _logger.LogWarning("Failed to normalize preference {PreferenceName}. Reason: {Reason}", preferenceName, normalizationResult.ErrorMessage);
        _snackbar.Add(normalizationResult.ErrorMessage ?? "Preference value is invalid.", Severity.Error);
        return;
      }

      var request = new UserPreferenceRequestDto(preferenceName, normalizationResult.Value ?? string.Empty);
      var setResult = await _controlrApi.Internal.UserPreferences.SetUserPreference(request);

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

  private static UserPreferencesDto CreateDefaultPreferences()
  {
    Dictionary<string, string> values = [];
    return UserPreferenceDefinitions.CreateDto(values);
  }
}