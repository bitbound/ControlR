namespace ControlR.Web.Client.Services;

/// <summary>
/// Server-side settings that the client needs to read in order to adapt the UI
/// (for example, hiding a button when a feature is disabled by configuration).
/// Backed by an HTTP call to <c>/api/internal/public-server-settings</c>.
/// </summary>
public interface IPublicServerSettingsProvider
{
  Task<PublicServerSettings> GetPublicServerSettings();
}
