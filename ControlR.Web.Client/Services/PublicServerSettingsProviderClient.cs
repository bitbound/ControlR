using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Web.Client.Services;

internal class PublicServerSettingsProviderClient(
  IControlrApi controlrApi,
  ILogger<PublicServerSettingsProviderClient> logger) : IPublicServerSettingsProvider
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<PublicServerSettingsProviderClient> _logger = logger;
  private Task<PublicServerSettings>? _cachedTask;

  public Task<PublicServerSettings> GetPublicServerSettings()
  {
    return _cachedTask ??= FetchAsync();
  }

  private async Task<PublicServerSettings> FetchAsync()
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

    return new PublicServerSettings(
      IsPublicRegistrationEnabled: false,
      DisableDesktopPreview: false);
  }
}
