namespace ControlR.Web.Client.Services;

/// <summary>
/// Resolves the origin the device (Responder) half of a relay session dials back to.
/// </summary>
/// <remarks>
/// A device is usually reachable from neither the browser's address-bar origin nor the reverse proxy
/// the browser happened to use, so the server's configured public origin is preferred. It is used
/// only when the server publishes one. Without one, this falls back to the browser origin, which is
/// what device-side relay URLs were built from before.
/// </remarks>
public interface IDeviceRelayUriResolver
{
  /// <summary>
  /// The server's public origin when it publishes one, otherwise the browser's own address.
  /// </summary>
  Task<Uri> ResolveBaseUriAsync();
}

internal class DeviceRelayUriResolver(
  IPublicServerSettingsProvider publicServerSettings,
  NavigationManager navManager) : IDeviceRelayUriResolver
{
  private readonly NavigationManager _navManager = navManager;
  private readonly IPublicServerSettingsProvider _publicServerSettings = publicServerSettings;

  public async Task<Uri> ResolveBaseUriAsync()
  {
    var settings = await _publicServerSettings.GetPublicServerSettings();

    return string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
      ? new Uri(_navManager.BaseUri)
      : new Uri(settings.PublicBaseUrl);
  }
}
