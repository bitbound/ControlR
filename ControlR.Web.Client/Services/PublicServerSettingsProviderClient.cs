namespace ControlR.Web.Client.Services;

internal class PublicServerSettingsProviderClient(
  IControlrApi controlrApi,
  ILogger<PublicServerSettingsProviderClient> logger) : IPublicServerSettingsProvider
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<PublicServerSettingsProviderClient> _logger = logger;
  private Task<InternalDtos.PublicServerSettings>? _cachedTask;

  public Task<InternalDtos.PublicServerSettings> GetPublicServerSettings()
  {
    return _cachedTask ??= FetchAsync();
  }

  private async Task<InternalDtos.PublicServerSettings> FetchAsync()
  {
    try
    {
      var result = await _controlrApi.Internal.PublicServerSettings.GetPublicServerSettings();
      if (result.IsSuccess)
      {
        return result.Value;
      }

      _logger.LogError("Failed to get public server settings: {Reason}", result.Reason);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting public server settings.");
    }

    return new InternalDtos.PublicServerSettings(
      IsPublicRegistrationEnabled: false,
      DisableDesktopPreview: false);
  }
}
