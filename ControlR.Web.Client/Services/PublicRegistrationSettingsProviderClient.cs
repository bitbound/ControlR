namespace ControlR.Web.Client.Services;
public interface IPublicRegistrationSettingsProvider
{
  Task<bool> GetIsPublicRegistrationEnabled();
}

internal class PublicRegistrationSettingsProviderClient(
  IControlrApi controlrApi,
  ILogger<PublicRegistrationSettingsProviderClient> logger) : IPublicRegistrationSettingsProvider
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<PublicRegistrationSettingsProviderClient> _logger = logger;
  private bool? _cachedValue;

  public async Task<bool> GetIsPublicRegistrationEnabled()
  {
    if (_cachedValue.HasValue)
    {
      return _cachedValue.Value;
    }

    try
    {
      var result = await _controlrApi.Internal.PublicRegistrationSettings.GetPublicRegistrationSettings();
      if (result.IsSuccess)
      {
        _cachedValue = result.Value.IsPublicRegistrationEnabled;
        return _cachedValue.Value;
      }

      _logger.LogError("Failed to get public registration settings: {Reason}", result.Reason);
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting public registration settings.");
      return false;
    }
  }
}
