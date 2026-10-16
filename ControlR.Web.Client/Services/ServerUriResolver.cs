namespace ControlR.Web.Client.Services;

/// <summary>
/// Resolves the origin clients should use to reach this server.
/// </summary>
/// <remarks>
/// A device or another client is often unable to reach the address this app was loaded from, so the
/// server's configured public origin is preferred. It is used only when the server publishes one.
/// Without one, this falls back to the browser origin, which is what callers used before.
/// </remarks>
public interface IServerUriResolver
{
  /// <summary>
  /// The server's public origin when it publishes one, otherwise the browser's own address.
  /// </summary>
  Task<Uri> ResolveBaseUriAsync();
}

internal class ServerUriResolver(
  IPublicServerSettingsProvider publicServerSettings,
  NavigationManager navManager) : IServerUriResolver
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
