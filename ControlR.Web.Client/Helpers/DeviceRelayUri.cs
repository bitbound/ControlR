namespace ControlR.Web.Client.Helpers;

/// <summary>
/// Resolves the origin the device (Responder) half of a relay session dials back to.
/// </summary>
/// <remarks>
/// The device is usually reachable from neither the browser's address-bar origin nor the reverse proxy
/// the browser happened to use, so the server's configured public origin is preferred. It is published
/// only when <c>AppOptions:PublicBaseUrl</c> is set to a usable origin. Without one, this falls back to
/// the browser origin, which is what device-side relay URLs were built from before.
/// </remarks>
public static class DeviceRelayUri
{
  /// <summary>
  /// Returns the public origin when the server publishes one, otherwise the browser origin.
  /// </summary>
  /// <param name="publicBaseUrl">The server's public origin, or <see langword="null"/> when unset.</param>
  /// <param name="browserBaseUri">The origin the app was loaded from, e.g. <c>NavigationManager.BaseUri</c>.</param>
  public static Uri ResolveBaseUri(string? publicBaseUrl, string browserBaseUri)
  {
    return string.IsNullOrWhiteSpace(publicBaseUrl)
      ? new Uri(browserBaseUri)
      : new Uri(publicBaseUrl!);
  }
}
